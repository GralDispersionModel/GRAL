#region Copyright
///<remarks>
/// <Graz Lagrangian Particle Dispersion Model>
/// Copyright (C) [2019]  [Dietmar Oettl, Markus Kuntner]
/// This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by
/// the Free Software Foundation version 3 of the License
/// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
/// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for more details.
/// You should have received a copy of the GNU General Public License along with this program.  If not, see <https://www.gnu.org/licenses/>.
///</remarks>
#endregion

using System;
using System.Diagnostics;
using System.Threading;

namespace GRAL_2001
{
    /// <summary>
    /// Ordered sweeps: the in-place sweeps of the microscale flow field solvers on all threads, with the result of one thread
    /// </summary>
    /// <remarks>
    /// What it is for:
    /// The pressure sweeps and the solvers for U, V, W and TKE update a field in place, column by column. The column (i1, j1)
    /// reads its own field at the four neighbours (i1 - 1, j1), (i1 + 1, j1), (i1, j1 - 1) and (i1, j1 + 1). When one thread
    /// sweeps over the columns, (i1 - 1, j1) and (i1, j1 - 1) are updated before the column and the other two after it, whether
    /// i1 or j1 is the outer loop. With several threads in a Parallel.For the values a column reads depend on the timing of the
    /// threads. For that reason the reproducible mode has run these sweeps on one thread.
    ///
    /// Why an ordered sweep is exact:
    /// An ordered sweep keeps the rule "a column is updated after (i1 - 1, j1) and (i1, j1 - 1) and before (i1 + 1, j1) and
    /// (i1, j1 + 1)" on any number of threads. Every column then reads the values it reads in the sweep on one thread, and the
    /// code of a column is the same, so the result is equal bit by bit for every number of threads. i1 and j1 are the sweep
    /// indices of the solver; the solver maps them to the cell indices i and j with its sweep direction (IS, JS).
    /// A solver can use an ordered sweep if a column writes its own column only, reads the field in work at the four neighbours
    /// only, and no other field it reads is changed during the sweep. Values that a solver carries from one column to the next
    /// of a line (PIMP and QIMP of the pressure sweep) must be kept per line.
    ///
    /// Strips and hand-over:
    /// The j1 lines are divided into one strip of neighbouring lines per thread, each strip with about the same number of
    /// columns to solve. Every thread goes through i1 in ascending order. In one step the thread solves the columns (i1, j1) of
    /// its strip and then publishes i1 as finished. A thread starts the step i1 only after the thread of the strip before it has
    /// published i1. So (i1 - 1, j1) was solved by the same thread one step earlier, (i1, j1 - 1) by the same thread or by the
    /// strip before, (i1 + 1, j1) follows in the next step, and (i1, j1 + 1) follows in the same thread or in the next strip,
    /// which is still waiting for this step. Neighbouring threads work one step apart; after the first steps all threads are busy.
    ///
    /// Threads:
    /// The strip 0 runs on the calling thread, the other strips on worker threads that are started once and used for all
    /// sweeps. A worker spins for a short time after a sweep and then parks, so it takes no processor time from the other
    /// parallel loops. The workers are background threads; Stop() ends them at the end of the run. An exception in a strip
    /// stops the other strips and is thrown again on the calling thread as an AggregateException, as Parallel.For does.
    /// Run() must not be called from two threads at the same time.
    /// </remarks>
    public static class OrderedSweep
    {
        /// <summary>
        /// Solves the columns i1First..i1Last x j1First..j1Last (sweep indices, inclusive) in the loop nest of the solver
        /// </summary>
        public delegate void Tile(int i1First, int i1Last, int j1First, int j1Last);

        /// <summary>
        /// Solves the columns j1First..j1Last (sweep indices, inclusive) of the line i1
        /// </summary>
        public delegate void Line(int i1, int j1First, int j1Last);

        //distance between the counters of two threads in the progress array: 16 longs = 128 bytes, no shared cache line
        private const int Pad = 16;
        //a sweep ticket holds the number of the sweep and, in the lowest bits, the number of its strips
        private const int StripBits = 16;
        //spins of a waiting thread before it yields the processor
        private const int SpinsBeforeYield = 4096;
        //time a worker spins after a sweep before it parks: 50 microseconds
        private static readonly long IdleSpinTicks = Math.Max(1, Stopwatch.Frequency / 20000);

        //columns to solve: number per line j, bounding box in cell indices; set by Prepare()
        private static int[] columnsOfLine = null;
        private static int boxIFirst = 1;
        private static int boxILast = int.MaxValue;
        private static int boxJFirst = 1;
        private static int boxJLast = int.MaxValue;

        //the sweep in work
        private static Tile sweepTile;
        private static int sweepI1First;
        private static int sweepI1Last;
        private static int[] stripBounds = new int[2];      //the strip s holds the lines stripBounds[s] .. stripBounds[s + 1] - 1
        private static long[] progress = new long[2 * Pad]; //last finished step i1 of the strip s at index (s + 1) * Pad

        //worker threads
        private static Thread[] workers = Array.Empty<Thread>();
        private static ManualResetEventSlim[] wakeUp = Array.Empty<ManualResetEventSlim>();
        private static long ticket;
        private static int pending;
        private static volatile bool abort;
        private static volatile bool stop;
        private static Exception failure;

        /// <summary>
        /// True if the flow field solvers use ordered sweeps: reproducible results are requested and more than one thread is available
        /// </summary>
        public static bool Enabled
        {
            get
            {
                return Program.UseFixedRndSeedVal && MaxThreads() > 1;
            }
        }

        /// <summary>
        /// Threads of an ordered sweep: Max_Proc, but not more than the logical processors of this process, because a waiting strip spins
        /// </summary>
        private static int MaxThreads()
        {
            return Math.Min(Math.Min(Program.IPROC, Environment.ProcessorCount), (1 << StripBits) - 1);
        }

        /// <summary>
        /// Defines the columns of the following sweeps; their number per line is used to balance the strips
        /// </summary>
        /// <param name="domain">Columns with domain[i][j] == 1 are solved; null: all columns are solved</param>
        public static void Prepare(byte[][] domain)
        {
            columnsOfLine = null;
            boxIFirst = 1;
            boxILast = int.MaxValue;
            boxJFirst = 1;
            boxJLast = int.MaxValue;
            if (domain == null || !Enabled)
            {
                return;
            }

            int NII = Program.NII;
            int NJJ = Program.NJJ;
            int[] columns = new int[NJJ + 2];
            boxIFirst = int.MaxValue;
            boxILast = 0;
            for (int i = 1; i <= NII; ++i)
            {
                byte[] domain_L = domain[i];
                int columnsOfI = 0;
                for (int j = 1; j <= NJJ; ++j)
                {
                    if (domain_L[j] == 1)
                    {
                        columns[j]++;
                        columnsOfI++;
                    }
                }
                if (columnsOfI > 0)
                {
                    boxIFirst = Math.Min(boxIFirst, i);
                    boxILast = i;
                }
            }
            boxJFirst = int.MaxValue;
            boxJLast = 0;
            for (int j = 1; j <= NJJ; ++j)
            {
                if (columns[j] > 0)
                {
                    boxJFirst = Math.Min(boxJFirst, j);
                    boxJLast = j;
                }
            }
            columnsOfLine = columns;
        }

        /// <summary>
        /// One ordered sweep of a solver with a loop body for a line i1
        /// </summary>
        public static void Run(int i1First, int i1Last, int j1First, int j1Last, int IS, int JS, Line line)
        {
            Run(i1First, i1Last, j1First, j1Last, IS, JS, (int first, int last, int j1A, int j1B) =>
            {
                for (int i1 = first; i1 <= last; ++i1)
                {
                    line(i1, j1A, j1B);
                }
            });
        }

        /// <summary>
        /// One ordered sweep over the columns i1First..i1Last x j1First..j1Last on all threads
        /// </summary>
        /// <param name="i1First">First sweep index in x direction</param>
        /// <param name="i1Last">Last sweep index in x direction (inclusive)</param>
        /// <param name="j1First">First sweep index in y direction</param>
        /// <param name="j1Last">Last sweep index in y direction (inclusive)</param>
        /// <param name="IS">Sweep direction for x cells: i = i1, or i = NII - i1 + 1 if IS is -1</param>
        /// <param name="JS">Sweep direction for y cells: j = j1, or j = NJJ - j1 + 1 if JS is -1</param>
        /// <param name="tile">Loop body of the solver for a range of columns</param>
        public static void Run(int i1First, int i1Last, int j1First, int j1Last, int IS, int JS, Tile tile)
        {
            //skip the lines without columns to solve
            if (boxIFirst > boxILast || boxJFirst > boxJLast)
            {
                return;
            }
            if (boxILast <= Program.NII)
            {
                i1First = Math.Max(i1First, IS == -1 ? Program.NII - boxILast + 1 : boxIFirst);
                i1Last = Math.Min(i1Last, IS == -1 ? Program.NII - boxIFirst + 1 : boxILast);
            }
            if (boxJLast <= Program.NJJ)
            {
                j1First = Math.Max(j1First, JS == -1 ? Program.NJJ - boxJLast + 1 : boxJFirst);
                j1Last = Math.Min(j1Last, JS == -1 ? Program.NJJ - boxJFirst + 1 : boxJLast);
            }
            if (i1First > i1Last || j1First > j1Last)
            {
                return;
            }

            int strips = Math.Min(MaxThreads(), j1Last - j1First + 1);
            if (strips <= 1)
            {
                //one thread: the loop nest of the solver over all columns is the sweep itself
                tile(i1First, i1Last, j1First, j1Last);
                return;
            }

            //strips with about the same number of columns to solve
            if (stripBounds.Length < strips + 1)
            {
                stripBounds = new int[strips + 1];
                progress = new long[(strips + 1) * Pad];
            }
            int[] columns = columnsOfLine;
            long columnsTotal = 0;
            for (int j1 = j1First; j1 <= j1Last; ++j1)
            {
                columnsTotal += columns == null ? 1 : columns[JS == -1 ? Program.NJJ - j1 + 1 : j1];
            }
            long columnsSum = 0;
            int strip = 1;
            stripBounds[0] = j1First;
            for (int j1 = j1First; j1 <= j1Last && strip < strips; ++j1)
            {
                columnsSum += columns == null ? 1 : columns[JS == -1 ? Program.NJJ - j1 + 1 : j1];
                //close the strip when it holds its share of the columns, or when each line left is needed for a strip of its own
                if (columnsSum * strips >= strip * columnsTotal || j1Last - j1 <= strips - strip)
                {
                    stripBounds[strip++] = j1 + 1;
                }
            }
            stripBounds[strips] = j1Last + 1;
            for (int s = 0; s <= strips; ++s)
            {
                progress[s * Pad] = i1First - 1;
            }

            StartWorkers(strips - 1);
            sweepTile = tile;
            sweepI1First = i1First;
            sweepI1Last = i1Last;
            failure = null;
            abort = false;
            Volatile.Write(ref pending, strips - 1);
            //the ticket is written before the events are set: a worker that parks resets its event first and then reads the ticket again
            Interlocked.Exchange(ref ticket, (((ticket >> StripBits) + 1) << StripBits) | (long)strips);
            for (int s = 1; s < strips; ++s)
            {
                wakeUp[s].Set();
            }

            try
            {
                SolveStrip(0);
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref failure, e, null);
                abort = true;
            }

            //wait for the other strips; they finish a few steps after the strip 0
            int spins = 0;
            while (Volatile.Read(ref pending) != 0)
            {
                if (++spins < SpinsBeforeYield)
                {
                    Thread.SpinWait(1);
                }
                else
                {
                    spins = 0;
                    Thread.Yield();
                }
            }
            sweepTile = null;
            if (failure != null)
            {
                throw new AggregateException(failure);
            }
        }

        /// <summary>
        /// Ends the worker threads; a later sweep starts new ones
        /// </summary>
        public static void Stop()
        {
            if (workers.Length == 0)
            {
                return;
            }
            stop = true;
            for (int s = 1; s <= workers.Length; ++s)
            {
                wakeUp[s].Set();
            }
            foreach (Thread worker in workers)
            {
                worker.Join();
            }
            workers = Array.Empty<Thread>();
            wakeUp = Array.Empty<ManualResetEventSlim>();
            stop = false;
        }

        /// <summary>
        /// All steps of one strip: the columns of the strip for each i1, after the strip before has finished this i1
        /// </summary>
        private static void SolveStrip(int strip)
        {
            Tile tile = sweepTile;
            long[] finished = progress;
            int i1Last = sweepI1Last;
            int j1First = stripBounds[strip];
            int j1Last = stripBounds[strip + 1] - 1;
            int before = strip * Pad;
            int own = (strip + 1) * Pad;
            long finishedBefore = long.MinValue;
            for (int i1 = sweepI1First; i1 <= i1Last; ++i1)
            {
                if (strip > 0 && finishedBefore < i1)
                {
                    int spins = 0;
                    while ((finishedBefore = Volatile.Read(ref finished[before])) < i1)
                    {
                        if (abort)
                        {
                            return;
                        }
                        if (++spins < SpinsBeforeYield)
                        {
                            Thread.SpinWait(1);
                        }
                        else
                        {
                            spins = 0;
                            Thread.Yield();
                        }
                    }
                }
                tile(i1, i1, j1First, j1Last);
                Volatile.Write(ref finished[own], i1);
            }
        }

        /// <summary>
        /// Starts worker threads until the given number exists; called between two sweeps only
        /// </summary>
        private static void StartWorkers(int count)
        {
            if (workers.Length >= count)
            {
                return;
            }
            Thread[] threads = new Thread[count];
            ManualResetEventSlim[] events = new ManualResetEventSlim[count + 1];
            Array.Copy(workers, threads, workers.Length);
            Array.Copy(wakeUp, events, wakeUp.Length);
            long seen = ticket;
            for (int w = workers.Length; w < count; ++w)
            {
                int strip = w + 1;
                events[strip] = new ManualResetEventSlim(false, 0);
                threads[w] = new Thread(() => WorkerLoop(strip, seen))
                {
                    IsBackground = true,
                    Name = "OrderedSweep " + strip.ToString()
                };
            }
            int started = workers.Length;
            wakeUp = events;
            workers = threads;
            for (int w = started; w < count; ++w)
            {
                threads[w].Start();
            }
        }

        /// <summary>
        /// A worker solves its strip in every sweep that has enough strips; between the sweeps it spins for a short time, then it parks
        /// </summary>
        /// <param name="strip">Strip of this worker</param>
        /// <param name="seen">Ticket of the last sweep before the worker was started</param>
        private static void WorkerLoop(int strip, long seen)
        {
            ManualResetEventSlim wake = wakeUp[strip];
            while (true)
            {
                long current;
                long idleSince = Stopwatch.GetTimestamp();
                int spins = 0;
                while ((current = Volatile.Read(ref ticket)) == seen)
                {
                    if (stop)
                    {
                        return;
                    }
                    if ((++spins & 15) != 0 || Stopwatch.GetTimestamp() - idleSince < IdleSpinTicks)
                    {
                        Thread.SpinWait(4);
                    }
                    else
                    {
                        //park; the ticket is read again after the reset, because the caller sets the event after it has written the ticket
                        wake.Reset();
                        if (Volatile.Read(ref ticket) == seen && !stop)
                        {
                            wake.Wait();
                        }
                        idleSince = Stopwatch.GetTimestamp();
                    }
                }
                seen = current;
                if (strip < (int)(current & ((1 << StripBits) - 1)))
                {
                    try
                    {
                        SolveStrip(strip);
                    }
                    catch (Exception e)
                    {
                        Interlocked.CompareExchange(ref failure, e, null);
                        abort = true;
                    }
                    Interlocked.Decrement(ref pending);
                }
            }
        }
    }
}
