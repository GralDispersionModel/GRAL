from __future__ import annotations
import argparse, hashlib, json, math, os, re, struct, subprocess, sys, time, zipfile
from pathlib import Path
from datetime import datetime, timedelta

DURATION = 30

def write(path, text):
    path.write_text(text, encoding='utf-8', newline='\n')

def make_case(folder, groups, factors, duration=DURATION, tps=100):
    folder.mkdir(parents=True, exist_ok=False)
    write(folder/'GRAL.geb', '\n'.join(['20', '20', '2,1.1', '10', '10', '1', ','.join(map(str,groups)), '0', '200', '0', '200'])+'\n')
    # TPS, TAUS, transient, meteo format, receptors, z0, latitude, meandering off,
    # pollutant, slice, grid dz, start, flow, output, result compression, no wait,
    # ASCII, roughness, subdomain, online, vector512, deterministic RNG.
    write(folder/'in.dat', '\n'.join([str(tps),str(duration),'0','4','0','0.1','47','Y','NOX','2','4','1','0','0','compressed V03','nokeystroke','ASCiiResults 0','0','0','0','0','1'])+'\n')
    write(folder/'Max_Proc.txt', '1\n')
    write(folder/'micro_vert_layers.txt', '40\n')
    write(folder/'cadastre.dat', 'x,y,z,dx,dy,dz,kg/h,u1,u2,u3,sg\n' + ''.join(f'{60+(i%3)*10},{80+(i%4)*10},1,8,8,2,1,0,0,0,{sg}\n' for i,sg in enumerate(groups)))
    write(folder/'meteopgt.all','10,1,10\nwind_dir,wind_speed,stability,frequency\n27,1,4,1\n')
    write(folder/'mettimeseries.dat',''.join(f'{(datetime(2022,1,1)+timedelta(hours=h)):%d.%m.%Y %H} 1 27 4\n' for h in range(len(factors))))
    write(folder/'emissions_timeseries.txt','date;hour;'+';'.join(map(str,groups))+'\n'+''.join((datetime(2022,1,1)+timedelta(hours=h)).strftime('%d.%m.%Y;%H;')+';'.join(str(row[sg]) for sg in groups)+'\n' for h,row in enumerate(factors)))
    write(folder/'GRAL_Trans_Conc_Threshold.txt', '1e-12\n')
    # Keep checkpoint output available for inspection, while each run starts in a fresh folder.
    write(folder/'KeepAndReadTransientTempFiles.dat', '1\n')
    return {'groups':groups, 'factors':factors, 'steps':len(factors), 'duration_seconds':duration, 'base_emission_kg_per_hour':1.0}

def token(sg):
    if sg < 100: return f'{sg:02d}'
    if sg < 360: return chr(65+(sg-100)//10)+str((sg-100)%10)
    if sg < 620: return str((sg-360)%10)+chr(65+(sg-360)//10)
    return chr(65+(sg-620)//26)+chr(65+(sg-620)%26)

def read_grids(folder, groups, steps):
    grids, raw = {}, {}
    for hour in range(1,steps+1):
        archive = folder/f'{hour:05d}.grz'
        if not archive.exists():
            raise AssertionError(f'missing {archive.name}')
        with zipfile.ZipFile(archive) as z:
            names = z.namelist()
            if len(names) != len(set(names)) or len(names) != len(groups):
                raise AssertionError(f'nonunique or wrong file count: {archive.name}: {len(names)}')
            for sg in groups:
                candidates = [f'{hour:05d}-1{token(sg)}.con']
                found = [n for n in candidates if n in names]
                if len(set(found)) != 1:
                    raise AssertionError(f'cannot map SG {sg}: {names[:3]}')
                payload = z.read(found[0])
                header,x,y,dx,dy,nx,ny = struct.unpack_from('<iiiffii',payload)
                if (header,nx,ny) != (-3,10,10) or len(payload) != 28+400:
                    raise AssertionError(f'bad grid header {found[0]}: {(header,nx,ny,len(payload))}')
                values = struct.unpack_from('<100f', payload,28)
                if not all(math.isfinite(v) and v>=0 for v in values):
                    raise AssertionError(f'invalid grid value {found[0]}')
                grids[(hour,sg)] = values
                raw[(hour,sg)] = hashlib.sha256(payload).hexdigest()
    return grids,raw

def run_case(root, name, dll, groups, factors, timeout=120, duration=DURATION, tps=100):
    folder=root/name
    spec=make_case(folder,groups,factors,duration,tps)
    start=time.monotonic()
    env=os.environ.copy()
    env.update({'DOTNET_CLI_TELEMETRY_OPTOUT':'1','TEMP':str(root),'TMP':str(root)})
    proc=subprocess.run(['dotnet',str(dll)],cwd=folder,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=timeout)
    output=proc.stdout.decode('utf-8',errors='replace')
    write(folder/'console.log',output)
    core=(folder/'Logfile_GRALCore.txt').read_text(encoding='utf-8',errors='replace') if (folder/'Logfile_GRALCore.txt').exists() else ''
    problem=(folder/'Problemreport_GRAL.txt').read_text(encoding='utf-8',errors='replace') if (folder/'Problemreport_GRAL.txt').exists() else ''
    result={'case':name,'elapsed_seconds':round(time.monotonic()-start,3),'exit_code':proc.returncode,'source_group_count':len(groups),'steps':len(factors),'duration_seconds':duration,'modeled_hours':len(factors)*duration/3600,'case_path':str(folder),'dll_sha256':hashlib.sha256(dll.read_bytes()).hexdigest()}
    assert proc.returncode==0, f'{name}: exit={proc.returncode}; {output[-3000:]}'
    assert 'GRAL simulations finished at:' in core, f'{name}: missing completed marker: {output[-3000:]}'
    assert not problem.strip(), f'{name}: problem report: {problem}'
    assert f'Source group count: {len(groups)}' in output
    assert f'Total number of area source partitions: {len(groups)}' in output
    assert 'Reading emissions_timeseries.txt successful' in output
    grids,raw=read_grids(folder,groups,len(factors))
    # GRAL prints cumulative released mass in kg with three significant figures.
    match=re.search(r'Summarized emission rates per source group in kg\s*\n([^\n]+)\n([^\n]+)',output)
    assert match, f'{name}: cumulative mass summary missing'
    labels=[int(v) for v in re.findall(r'SG:\s*(\d+)',match.group(1))]
    masses=[float(v) for v in match.group(2).split()]
    assert labels==groups and len(masses)==len(groups),f'{name}: mass SG order wrong'
    for sg,measured in zip(groups,masses):
        expected=sum(row[sg] for row in factors)*duration/3600
        assert math.isclose(measured,expected,rel_tol=.006,abs_tol=1e-9),f'{name}: SG{sg} released {measured}, expected {expected}'
    result['released_mass_kg_by_sg']={str(sg):mass for sg,mass in zip(groups,masses)}
    result['concentration_sum_by_step_sg']={f'{h}:{sg}':sum(grids[(h,sg)]) for h in range(1,len(factors)+1) for sg in groups}
    result['grid_payload_sha256']={f'{h}:{sg}':raw[(h,sg)] for h in range(1,len(factors)+1) for sg in groups}
    result['status']='pass'
    write(folder/'case_result.json',json.dumps(result,indent=2))
    print(f'[PASS] {name} groups={len(groups)} steps={len(factors)} seconds={result["elapsed_seconds"]}',flush=True)
    return result,grids,raw

def expect_rejection(root,name,dll,groups,marker):
    folder=root/name
    make_case(folder,groups,[{sg:1 for sg in groups}])
    proc=subprocess.run(['dotnet',str(dll)],cwd=folder,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=10)
    output=proc.stdout.decode('utf-8',errors='replace')
    write(folder/'console.log',output)
    assert marker in output,f'{name}: expected rejection marker missing: {output[-1000:]}'
    assert not list(folder.glob('*.grz')),f'{name}: rejected input produced results'
    result={'name':name,'status':'pass','expected_rejection':True,'exit_code':proc.returncode,'rejection_marker':marker,'groups':groups,'dll_sha256':hashlib.sha256(dll.read_bytes()).hexdigest()}
    write(folder/'case_result.json',json.dumps(result,indent=2))
    print(f'[PASS] {name} expected_rejection=True',flush=True)
    return result

def summarize(report):
    cases=[]
    for r in report['cases']:
        compact={k:v for k,v in r.items() if k not in ['released_mass_kg_by_sg','concentration_sum_by_step_sg','grid_payload_sha256']}
        compact['grid_payload_count']=len(r['grid_payload_sha256'])
        compact['grid_manifest_sha256']=hashlib.sha256(json.dumps(r['grid_payload_sha256'],sort_keys=True).encode()).hexdigest()
        masses=list(r['released_mass_kg_by_sg'].values())
        compact['released_mass_kg_total']=sum(masses)
        compact['released_mass_kg_min']=min(masses)
        compact['released_mass_kg_max']=max(masses)
        cases.append(compact)
    return {**report,'cases':cases}

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--original',type=Path,required=True)
    p.add_argument('--patched',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--pilot',action='store_true')
    a=p.parse_args()
    root=a.output.resolve();root.mkdir(parents=True,exist_ok=False)
    report={'status':'running','cases':[],'checks':[],'limits':['Flat synthetic domain, no buildings or deposition.','30-second transient tests validate routing, scaling, carryover, and output; a separate 101-step test uses 3600 seconds per step with synthetic meteorology. These are not scientific production simulations.','GUI creation and GUI result rendering are outside this core test.']}
    try:
        if a.pilot:
            groups=[1,2]
            rows=[{1:1,2:0},{1:0,2:1},{1:0,2:0}]
            r,g,h=run_case(root,'pilot',a.original,groups,rows)
            report['cases'].append(r)
        else:
            groups=list(range(1,100)); rows=[{sg:v for sg in groups} for v in [1,.5,0]]
            r0,g0,h0=run_case(root,'original_99',a.original,groups,rows);report['cases'].append(r0)
            r1,g1,h1=run_case(root,'patched_99',a.patched,groups,rows);report['cases'].append(r1)
            assert h0==h1,'legacy 99-source-group concentration payloads differ'
            report['checks'].append({'name':'legacy_99_bitwise_grid_regression','status':'pass','grid_payloads_compared':len(h0)})
            for label,invalid in [('duplicate',[1,1]),('zero',[0]),('negative',[-1]),('overflow',[2147483648]),('filename_limit',[1296])]:
                report['checks'].append(expect_rejection(root,'patched_reject_'+label,a.patched,invalid,'Source groups must be unique IDs in 1..1295'))
            for count in [100,300,1295]:
                groups=list(range(1,count+1)); active=[1,100] if count==100 else [1,100,256,count]
                rows=[{sg:int(sg==selected) for sg in groups} for selected in active]
                rows.append({sg:0 for sg in groups})
                r,g,h=run_case(root,f'patched_{count}',a.patched,groups,rows);report['cases'].append(r)
                for step,sg in enumerate(active,1):
                    assert sum(g[(step,sg)])>0,f'SG {sg} was not released at step {step}'
                    if step>1: assert sum(g[(step-1,sg)])==0,f'SG {sg} emitted early'
                    assert sum(g[(step+1,sg)])>0,f'SG {sg} lost transient carryover'
                never=set(groups)-set(active)
                assert all(sum(g[(step,sg)])==0 for step in range(1,len(rows)+1) for sg in never),'inactive SG contamination'
                report['checks'].append({'name':f'{count}_groups_temporal_routing_mass_and_carryover','status':'pass','active_sg_in_order':active,'zero_inactive_groups':len(never)})
            # Sparse identifiers test the filename boundaries independently from group count.
            groups=[1,99,100,359,360,619,620,1001,1295]
            rows=[{sg:1 for sg in groups},{sg:0 for sg in groups}]
            r,g,h=run_case(root,'patched_sparse_high_ids',a.patched,groups,rows);report['cases'].append(r)
            assert all(sum(g[(1,sg)])>0 and sum(g[(2,sg)])>0 for sg in groups)
            report['checks'].append({'name':'sparse_identifiers_through_1295','status':'pass','groups':groups})
            groups=list(range(1,102))
            rows=[{sg:int(sg==selected) for sg in groups} for selected in groups]
            r,g,h=run_case(root,'patched_101_hourly_groups',a.patched,groups,rows,duration=3600,tps=1)
            report['cases'].append(r)
            for step,sg in enumerate(groups,1):
                assert sum(g[(step,sg)])>0,f'SG {sg} was not released at hourly step {step}'
                assert all(sum(g[(earlier,sg)])==0 for earlier in range(1,step)),f'SG {sg} emitted before assigned hour'
            report['checks'].append({'name':'101_distinct_groups_101_full_hourly_steps','status':'pass','modeled_hours':101,'weather_steps':101,'grid_payload_count':len(h),'each_group_cumulative_release_kg':1})
        report['status']='pass'
    except Exception as ex:
        report['status']='fail';report['error']=repr(ex)
        raise
    finally:
        write(root/'validation_report.json',json.dumps(report,indent=2))
        write(root/'validation_summary.json',json.dumps(summarize(report),indent=2))
    print('[PASS] validation_report.json',flush=True)

if __name__=='__main__': main()