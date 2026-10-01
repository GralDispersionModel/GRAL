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
    p=argparse.ArgumentParser();p.add_argument('--baseline',type=Path,required=True);p.add_argument('--patched',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    root=a.output.resolve();root.mkdir(parents=True,exist_ok=False);reports=[];checks=[]
    for steady in [False,True]:
        for receptors in [0,2,255,256,1000]:
            pair=[]
            for label,dll in [('baseline',a.baseline.resolve()),('patched',a.patched.resolve())]:
                folder=root/f'{label}_{steady}_{receptors}'
                spec=make_case(folder,[{1:1,2:.5},{1:0,2:2},{1:1,2:0}],kinds=('PS','TS','LS','AS'),deposition=True,steady=steady,tps=20)
                if receptors:
                    lines=(folder/'in.dat').read_text().splitlines();lines[4]='1';write(folder/'in.dat','\n'.join(lines)+'\n')
                    write(folder/'Receptor.dat',str(receptors)+'\n'+''.join(f'R{i},{65+(i%10)*6},{75+(i%12)*5},2\n' for i in range(receptors)))
                result=execute(dll,folder,spec)
                result['receptors']={n:hashlib.sha256((folder/n).read_bytes()).hexdigest() for n in ['ReceptorConcentrations.dat','Receptor_Timeseries_Transient.txt'] if (folder/n).exists()}
                if receptors: assert result['receptors'],'missing receptor result'
                pair.append(result);reports.append(result)
            assert pair[0]['payloads']==pair[1]['payloads'],'concentration/deposition regression'
            assert pair[0]['receptors']==pair[1]['receptors'],'receptor regression'
            checks.append(dict(steady=steady,receptors=receptors,payloads=len(pair[0]['payloads']),receptor_files=len(pair[0]['receptors'])))
    write(root/'validation.json',json.dumps(dict(status='pass',runs=len(reports),checks=checks,reports=reports),indent=2));print('PASS all',len(checks))
if __name__=='__main__':main()
