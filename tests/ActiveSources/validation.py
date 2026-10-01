from __future__ import annotations
import argparse, hashlib, json, math, os, re, shutil, struct, subprocess, time, zipfile
from pathlib import Path
from datetime import datetime, timedelta


def write(path, text):
    path.write_text(text, encoding='utf-8', newline='\n')


def make_case(folder, factors, kinds=('AS',), rates=(1,1), ids=(1,2), duration=30, tps=100, deposition=False, steady=False, workers=1, start=1, temporal=True, fixed_seed=True):
    folder.mkdir(parents=True, exist_ok=False)
    groups=list(dict.fromkeys(ids))
    write(folder/'GRAL.geb', '\n'.join(['20','20','2,1.1','10','10','1',','.join(map(str,groups)),'0','200','0','200'])+'\n')
    write(folder/'in.dat', '\n'.join([str(tps),str(duration),str(int(steady)),'4','0','0.1','47','Y','NOX','2','4',str(start),'0','0','compressed V03','nokeystroke','ASCiiResults 0','0','0','0','0',str(int(fixed_seed))])+'\n')
    write(folder/'Max_Proc.txt',str(workers)+'\n'); write(folder/'micro_vert_layers.txt','40\n')
    write(folder/'meteopgt.all','10,1,10\nwind_dir,wind_speed,stability,frequency\n27,1,4,1\n')
    write(folder/'mettimeseries.dat',''.join(f'{datetime(2022,1,1)+timedelta(hours=h):%d.%m.%Y %H} 1 27 4\n' for h in range(len(factors))))
    if temporal:
        write(folder/'emissions_timeseries.txt','date;hour;'+';'.join(map(str,groups))+'\n'+''.join(f'{datetime(2022,1,1)+timedelta(hours=h):%d.%m.%Y;%H};'+';'.join(str(row[g]) for g in groups)+'\n' for h,row in enumerate(factors)))
    write(folder/'GRAL_Trans_Conc_Threshold.txt','1e-12\n'); write(folder/'KeepAndReadTransientTempFiles.dat','1\n')
    for kind in kinds:
        rows=[]
        for i,(rate,sg) in enumerate(zip(rates,ids)):
            x=60+(i%3)*10; y=80+(i%4)*10
            if kind=='PS': row=f'{x},{y},3,{rate},0,0,0,1,1,293,{sg}'
            elif kind=='LS': row=f'1,1,{sg},{x},{y},1,{x+10},{y},1,2,2,0,0,{rate*100}'
            elif kind=='TS': row=f'{x},{y},{x+10},{y},1,3,{rate},0,0,0,{sg}'
            else: row=f'{x},{y},1,8,8,2,{rate},0,0,0,{sg}'
            if deposition:
                if kind=='LS': row+=',0,0,0,0,0'
                row+=',25,70,20,2000,0.001,0.005,0.01,1'
                if kind=='TS': row+=',0,1'
            elif kind=='TS': row+=',0,1'
            rows.append(row)
        filename,headers={'PS':('point.dat',2),'LS':('line.dat',5),'TS':('portals.dat',2),'AS':('cadastre.dat',1)}[kind]
        write(folder/filename, ('header\n'*headers)+'\n'.join(rows)+'\n')
    return dict(groups=groups,factors=factors,kinds=kinds,rates=rates,ids=ids,duration=duration,steady=steady,start=start,temporal=temporal)


def execute(dll, folder, spec, timeout=120, extra=()):
    before=time.perf_counter()
    p=subprocess.run(['dotnet',str(dll),str(folder),*extra],cwd=folder,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout)
    log=p.stdout.decode('utf-8',errors='replace'); write(folder/'console.log',log)
    assert p.returncode==0, f'{folder.name}: {p.returncode}: {log[-1500:]}'
    assert 'GRAL simulations finished at:' in (folder/'Logfile_GRALCore.txt').read_text(encoding='utf-8-sig'), folder.name+' missing finish'
    problem=folder/'Problemreport_GRAL.txt'
    assert not problem.exists() or not problem.read_text(encoding='utf-8-sig').strip(), folder.name+' problem report'
    payloads={}; sums={}
    for zip_path in sorted(folder.glob('*.grz')):
        with zipfile.ZipFile(zip_path) as z:
            for name in z.namelist():
                data=z.read(name); payloads[name]=hashlib.sha256(data).hexdigest()
                if name.endswith(('.con','.dep')):
                    header,x,y,dx,dy,nx,ny=struct.unpack_from('<iiiffii',data)
                    assert header==-3 and len(data)==28+nx*ny*4,(name,len(data),nx,ny)
                    values=struct.unpack_from(f'<{nx*ny}f',data,28)
                    assert all(math.isfinite(v) and v>=0 for v in values),name+' invalid values'
                    sums[name]=sum(values)
    assert payloads,folder.name+' no output'
    mass={}
    if not spec['steady']:
        matches=re.findall(r'Summarized emission rates per source group in kg\s*\n([^\n]+)\n([^\n]+)',log)
        match=(None,*matches[-1]) if matches else None
        assert match, folder.name+' mass summary'
        labels=[int(v) for v in re.findall(r'SG:\s*(\d+)',match[1])]; values=list(map(float,match[2].split()))
        assert labels==spec['groups']
        for sg,value in zip(labels,values):
            base=sum(rate for rate,g in zip(spec['rates'],spec['ids']) if g==sg)*len(spec['kinds'])
            factor=sum(row[sg] if spec['temporal'] else 1 for row in spec['factors'][spec['start']-1:])
            expected=base*factor*spec['duration']/3600
            assert math.isclose(value,expected,rel_tol=.006,abs_tol=1e-9),(folder.name,sg,value,expected)
            mass[str(sg)]=value
    report=dict(name=folder.name,status='pass',seconds=time.perf_counter()-before,allocations=list(map(int,re.findall(r'Using a total of (\d+) particles',log))),mass_kg=mass,payload_count=len(payloads),payloads=payloads,sums=sums,dll_sha256=hashlib.sha256(dll.read_bytes()).hexdigest())
    write(folder/'result.json',json.dumps(report,indent=2)); print('PASS',folder.name,'allocations',report['allocations'],flush=True)
    return report


def main():
    p=argparse.ArgumentParser(); p.add_argument('--baseline',type=Path,required=True);p.add_argument('--patched',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args();a.baseline=a.baseline.resolve();a.patched=a.patched.resolve()
    root=a.output.resolve();root.mkdir(parents=True,exist_ok=False)
    reports=[];checks=[]
    def run(name,dll,rows,**kwargs):
        folder=root/name;spec=make_case(folder,rows,**kwargs);r=execute(dll,folder,spec);reports.append(r);return r
    try:
        # All-positive factors retain the original allocation and per-particle mass.
        for dep in [False,True]:
            rows=[{1:1,2:1},{1:.25,2:2},{1:0,2:0}]
            old=run('positive_baseline_'+str(dep),a.baseline,rows,kinds=('PS','LS','TS','AS'),deposition=dep)
            new=run('positive_patched_'+str(dep),a.patched,rows,kinds=('PS','LS','TS','AS'),deposition=dep)
            assert old['payloads']==new['payloads'],'all-positive payload regression '+str(dep)
            checks.append(dict(name='all_positive_bitwise_deposition_'+str(dep),payloads=len(new['payloads'])))
        old=run('steady_baseline',a.baseline,[{1:1,2:0}],kinds=('PS','LS','TS','AS'),steady=True)
        new=run('steady_patched',a.patched,[{1:1,2:0}],kinds=('PS','LS','TS','AS'),steady=True)
        assert old['payloads']==new['payloads'];checks.append(dict(name='steady_bitwise',payloads=len(new['payloads'])))
        rows=[{1:1,2:0},{1:0,2:1},{1:0,2:0},{1:.5,2:2}]
        for kind in ['PS','LS','TS','AS']:
            new=run('switch_'+kind,a.patched,rows,kinds=(kind,),rates=(2,0,5,3),ids=(1,1,2,1))
            assert new['allocations']==[3000,3000,0,3000]
            assert new['sums']['00001-102.con']==0 and new['sums']['00002-102.con']>0
            assert new['sums']['00003-102.con']>0,'all-off lost carryover'
            checks.append(dict(name='switch_zero_source_mass_carryover_'+kind))
        new=run('inactive_first_deposition',a.patched,[{1:0,2:0},{1:0,2:1},{1:0,2:0},{1:1,2:0}],kinds=('PS','LS','TS','AS'),deposition=True)
        assert new['allocations'][0]==0 and new['allocations'][2]==0
        assert all(v==0 for n,v in new['sums'].items() if n.startswith('00001-'))
        assert new['sums']['00003-102.con']>0 and new['sums']['00003-02.dep']>0
        checks.append(dict(name='inactive_first_group_retains_transient_deposition'))
        new=run('zero_base_all_types',a.patched,[{1:1,2:1}]*2,kinds=('PS','LS','TS','AS'),rates=(0,0))
        assert new['allocations']==[0,0] and all(v==0 for v in new['sums'].values())
        checks.append(dict(name='zero_base_no_overflow'))
        new=run('minimum_six_particles',a.patched,[{1:1,2:0},{1:0,2:1},{1:0,2:0}],tps=.01)
        assert new['allocations']==[6,6,0];checks.append(dict(name='small_deterministic_progress'))
        new=run('two_workers',a.patched,rows,kinds=('PS','LS','TS','AS'),rates=(2,0,5,3),ids=(1,1,2,1),workers=2)
        assert new['allocations']==[3000,3000,0,3000];checks.append(dict(name='parallel_two_workers_mass_and_allocation'))
        new=run('no_timeseries_zero_source',a.patched,[{1:1,2:1}]*2,rates=(1,0),temporal=False)
        assert new['allocations']==[3000,3000];checks.append(dict(name='no_temporal_file_zero_base'))
        new=run('default_rng_parallel',a.patched,rows,kinds=('PS','LS','TS','AS'),rates=(2,0,5,3),ids=(1,1,2,1),workers=2,fixed_seed=False)
        assert new['allocations']==[3000,3000,0,3000];checks.append(dict(name='default_rng_parallel_mass_and_allocation'))
        # A direct restart at hour 2 exercises the current row, not row 1.
        new=run('start_second_hour',a.patched,rows,start=2)
        assert new['allocations']==[3000,0,3000];checks.append(dict(name='nonfirst_start_index'))
        # Removing inactive input records must leave the active first-step plume unchanged.
        one=[{1:1,2:0}]
        full=run('active_geometry_full',a.patched,one,kinds=('PS','LS','TS','AS'),rates=(2,0,5,3),ids=(1,1,2,1))
        folder=root/'active_geometry_filtered'
        spec=make_case(folder,one,kinds=('PS','LS','TS','AS'),rates=(2,0,5,3),ids=(1,1,2,1))
        for filename,headers in [('point.dat',2),('line.dat',5),('portals.dat',2),('cadastre.dat',1)]:
            lines=(folder/filename).read_text().splitlines()
            write(folder/filename,'\n'.join(lines[:headers]+[lines[headers],lines[headers+3]])+'\n')
        filtered=execute(a.patched,folder,spec);reports.append(filtered)
        assert full['payloads']==filtered['payloads'];checks.append(dict(name='active_geometry_matches_physically_filtered_inputs',payloads=len(full['payloads'])))
        # Resume one identical checkpoint with both implementations during an all-off hour.
        rows=[{1:1,2:0},{1:0,2:1},{1:1,2:0},{1:0,2:0},{1:0,2:0}]
        folder=root/'checkpoint_seed';spec=make_case(folder,rows,kinds=('PS','LS','TS','AS'),deposition=True)
        write(folder/'KeepAndReadTransientTempFiles.dat','2\n')
        seeded=execute(a.patched,folder,spec);reports.append(seeded)
        resumes=[]
        for label,dll in [('old',a.baseline),('new',a.patched)]:
            target=root/('checkpoint_resume_'+label);shutil.copytree(folder,target)
            # The keep/read override restarts at in.dat's start; remove it only in this fixture
            # to exercise automatic resume after the saved checkpoint.
            (target/'KeepAndReadTransientTempFiles.dat').unlink()
            resumed=execute(dll,target,spec);reports.append(resumed)
            assert 'Reading Transient_Concentrations.tmp successful!' in (target/'console.log').read_text()
            assert re.findall(r'Weather number: (\d+)', (target/'console.log').read_text())[0]=='5'
            resumes.append(resumed)
        assert resumes[0]['payloads']==resumes[1]['payloads'],'equivalent all-off checkpoint changed'
        assert resumes[1]['allocations']==[0],'resumed all-off hour allocated source particles'
        assert resumes[1]['sums']['00005-101.con']>0 and resumes[1]['sums']['00005-01.dep']>0
        checks.append(dict(name='equivalent_checkpoint_all_off_carryover_and_deposition',payloads=len(resumes[1]['payloads']),continuous_equals_resumed=seeded['payloads']==resumes[1]['payloads']))
        status='pass'
    except Exception as ex:
        status='fail';checks.append(dict(error=repr(ex)));raise
    finally:
        summary=dict(status=status,cases=[{k:v for k,v in r.items() if k not in ['payloads','sums']} for r in reports],checks=checks,scope='Small flat synthetic cases; runtime and physical convergence on a production domain are not established.')
        write(root/'validation.json',json.dumps(summary,indent=2))
    print('PASS integration cases',len(reports),'checks',len(checks),flush=True)

if __name__=='__main__':main()
