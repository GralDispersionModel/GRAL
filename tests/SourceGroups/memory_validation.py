
import shutil
import argparse, hashlib, json, os, statistics, subprocess, time, zipfile
from pathlib import Path
import validation as v

def payloads(folder):
    result={}
    for path in sorted(folder.glob('*.grz')):
        with zipfile.ZipFile(path) as z:
            for name in z.namelist():
                result[path.name+'/'+name]=hashlib.sha256(z.read(name)).hexdigest()
    return result

def execute(dll,folder,label):
    start=time.perf_counter()
    p=subprocess.run(['dotnet',str(dll)],cwd=folder,capture_output=True,timeout=120)
    text=p.stdout.decode('utf-8',errors='replace')+p.stderr.decode('utf-8',errors='replace')
    (folder/(label+'.log')).write_text(text,encoding='utf-8')
    assert p.returncode==0 and 'GRAL simulations finished at:' in (folder/'Logfile_GRALCore.txt').read_text(errors='replace')
    assert not (folder/'Problemreport_GRAL.txt').exists() or not (folder/'Problemreport_GRAL.txt').read_text().strip()
    return time.perf_counter()-start,text

def main():
    p=argparse.ArgumentParser()
    for key in ['dense','patched','harness','output']:p.add_argument('--'+key,type=Path,required=True)
    a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
    report={'status':'running','storage':[],'comparisons':[],'restarts':[]}
    try:
        for groups in [99,300,1295]:
            for occupied in [1,groups]:
                measurements=[]
                for mode in ['dense','blocks']:
                    rows=[]
                    for repeat in range(3):
                        proc=subprocess.run(['dotnet',str(a.harness),'--memory',mode,str(groups),str(occupied),'20000'],capture_output=True,text=True,check=True,timeout=60)
                        rows.append(json.loads(proc.stdout))
                    measurements.append({'mode':mode,'median_managed_bytes':statistics.median(x['managed_bytes'] for x in rows),'median_elapsed_ms':statistics.median(x['elapsed_ms'] for x in rows),'runs':rows})
                report['storage'].append({'groups':groups,'occupied_groups_per_cell':occupied,'measurements':measurements})
                print('[PASS] storage',groups,occupied,flush=True)
        for case,count,dense_plume,steady,odour in [
            ('transient_sparse',1295,False,False,False),
            ('transient_dense',1295,True,False,False),
            ('steady',300,True,True,False),
            ('odour',300,True,False,True)]:
            groups=list(range(1,count+1))
            rows=[{g:float(dense_plume or g==active) for g in groups} for active in [1,count,0,100,0]]
            reference=None;times={}
            for mode,dll in [('dense',a.dense),('blocks',a.patched)]:
                samples=[]
                for repeat in range(3):
                    folder=out/f'{case}_{mode}_{repeat}'
                    v.make_case(folder,groups,rows)
                    if steady:
                        lines=(folder/'in.dat').read_text().splitlines();lines[2]='1';v.write(folder/'in.dat','\n'.join(lines)+'\n')
                    if odour:v.write(folder/'Pollutant.txt','odour\n0\n0\n0\n')
                    elapsed,_=execute(dll,folder,'run');samples.append(elapsed)
                    data=payloads(folder);assert data
                    if reference is None:reference=data
                    assert data==reference,f'{case} {mode} output mismatch'
                times[mode]=samples
            report['comparisons'].append({'case':case,'status':'pass','groups':count,'equal_payloads':len(reference),'elapsed_seconds':times,'medians':{m:statistics.median(t) for m,t in times.items()}})
            print('[PASS] simulation',case,'payloads',len(reference),flush=True)
        # Read an earlier checkpoint with identical full inputs, rerun the final step.
        for label,first,second in [('dense_to_blocks',a.dense,a.patched),('blocks_to_dense',a.patched,a.dense)]:
            folder=out/label;groups=list(range(1,301))
            rows=[{g:float(g==active) for g in groups} for active in [1,300,100,0,0]]
            v.make_case(folder,groups,rows);v.write(folder/'KeepAndReadTransientTempFiles.dat','2\n')
            execute(first,folder,'initial');expected=payloads(folder)
            reference_folder=out/(label+'_reference')
            shutil.copytree(folder,reference_folder)
            # Compare equivalent restarts: existing checkpoint filtering can differ from an uninterrupted run.
            execute(first,reference_folder,'resume_reference')
            expected_resumed=payloads(reference_folder)
            before=(folder/'00005.grz').stat().st_mtime_ns
            _,log=execute(second,folder,'resume')
            assert 'Reading Transient_Concentrations.tmp successful!' in log
            assert (folder/'00005.grz').stat().st_mtime_ns!=before,'final step was not recomputed'
            assert payloads(folder)==expected_resumed,'dense and block restart payloads differ'
            report['restarts'].append({'case':label,'status':'pass','groups':300,'resumed_step':5,'equal_final_payloads':300,'continuous_equals_resumed':expected==expected_resumed})
            print('[PASS] restart',label,flush=True)
        report['status']='pass'
    except Exception as ex:
        report['status']='fail';report['error']=repr(ex);raise
    finally:(out/'memory_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
if __name__=='__main__':main()

