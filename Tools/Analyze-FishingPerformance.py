"""Summarize actual fixed-tour CSV rows; no inferred GPU timings or setup-frame mixing."""
from pathlib import Path
import argparse,csv,json,math
import contextlib,io,tempfile

def valid(value):
    try:
        number=float(value)
        return number if math.isfinite(number) and number>0 else None
    except (TypeError,ValueError):
        return None

def timings(values):
    values=sorted(x for value in values if (x:=valid(value)) is not None)
    if not values:
        return {"valid":0,"p50":None,"p95":None,"p99":None,"over50":None,"over100":None}
    return {"valid":len(values),**{f"p{p}":values[math.ceil(len(values)*p/100)-1] for p in (50,95,99)},
            "over50":sum(x>50 for x in values),"over100":sum(x>100 for x in values)}

def analyze(folder):
    folder=Path(folder)
    tours=list(folder.glob("*.tour.json"))
    assert len(tours)==1,f"Expected one tour manifest in {folder}"
    tour=json.loads(tours[0].read_text(encoding="utf-8-sig"))
    csv_path=tours[0].with_suffix("").with_suffix(".csv")
    assert csv_path.exists(),f"Missing copied performance CSV: {csv_path}"
    rows=list(csv.DictReader(csv_path.open(encoding="utf-8-sig",newline="")))
    assert len(tour["phases"])==18,"The native tour did not finish two continuous approaches and sixteen steady segments"
    assert (tour["width"],tour["height"])==(1920,1080),"Comparison needs actual 1080p frames"
    launch=json.loads((folder/"launch.json").read_text(encoding="utf-8-sig")) if (folder/"launch.json").exists() else {}
    phases=[];all_rows=[];continuous_rows=[];steady_rows=[]
    previous_end=None
    distance=lambda first,last:math.hypot(float(last["x"])-float(first["x"]),float(last["z"])-float(first["z"]))
    for index,phase in enumerate(tour["phases"]):
        continuous=phase["continuous"]
        assert continuous==(index<2),"The first two measured phases must be uninterrupted first approaches"
        selected=[row for row in rows if int(row["segment_id"])==phase["segment"]]
        assert len(selected)>=30,f"Insufficient real measured frames in {phase['name']}"
        assert float(selected[-1]["seconds"])-float(selected[0]["seconds"])>=9,f"Short measured phase: {phase['name']}"
        recent=sum(0<=int(row["frame"])-int(row["world_camera_frame"])<=2 for row in selected)
        assert recent/len(selected)>=.95,f"Primary camera did not actually render for enough frames: {phase['name']}"
        rendered=len({int(row["world_camera_frame"]) for row in selected if int(row["world_camera_frame"])>0})
        assert rendered/len(selected)>=.95,f"Primary camera renders were reused across too many simulation frames: {phase['name']}"
        expected=phase["riflePresented"]
        assert all(bool(int(row["state_flags"])&2)==expected for row in selected),f"Rifle visibility changed during {phase['name']}"
        if continuous:
            assert not expected and all(int(row["state_flags"])&13==0 for row in selected),"Continuous sailing must stay afloat, unpaused and out of menus"
            assert phase["travelledMetres"]>=50 and distance(phase["end"],phase["target"])<=8.1,"Continuous approach did not reach its actual destination"
            if previous_end is not None:
                assert distance(previous_end,phase["start"])<=2,"Continuous approaches contain a transition teleport"
            for first,last in zip(selected,selected[1:]):
                assert distance(first,last)<=2+float(last["wall_ms"])*.02,"Continuous sailing contains an implausible position jump"
            previous_end=phase["end"];continuous_rows.extend(selected)
        else:
            steady_rows.extend(selected)
        all_rows.extend(selected)
        phases.append({"name":phase["name"],"continuous":continuous,"frames":len(selected),"renderedCameraFrames":rendered,"travelledMetres":phase["travelledMetres"],
            **{name:timings(row[column] for row in selected) for name,column in (("wall","wall_ms"),("cpu","cpu_frame_ms"),("main","cpu_main_ms"),("render","cpu_render_ms"),("gpu","gpu_ms"))},
            "allocatedBytes":sum(int(row["main_thread_allocated_bytes"]) for row in selected),
            "gc":[sum(int(row[f"gc{n}"]) for row in selected) for n in range(3)]})
    result={"version":tour["version"],"description":tour["description"],"runtimeSceneReadySeconds":tour["runtimeSceneReadySeconds"],"processLaunchToReadySeconds":launch.get("processLaunchToReadySeconds"),"phases":phases,
        "measuredFrames":len(all_rows),"wall":timings(row["wall_ms"] for row in all_rows),"gpu":timings(row["gpu_ms"] for row in all_rows),
        "continuousWall":timings(row["wall_ms"] for row in continuous_rows),"steadyWall":timings(row["wall_ms"] for row in steady_rows),
        "allocatedBytes":sum(int(row["main_thread_allocated_bytes"]) for row in all_rows),
        "gc":[sum(int(row[f"gc{n}"]) for row in all_rows) for n in range(3)],
        "largestWallFrames":[{key:row[key] for key in ("segment_id","frame","wall_ms","cpu_main_ms","gpu_ms","main_thread_allocated_bytes","gc0","x","z")}
            for row in sorted(all_rows,key=lambda row:float(row["wall_ms"]),reverse=True)[:20]]}
    (folder/"summary.json").write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding="utf8")
    number=lambda value:"NA" if value is None else f"{value:.2f}"
    lines=[f"Process launch to scene ready: {number(result['processLaunchToReadySeconds'])} s (OS/storage caches are not forcibly flushed)",f"Runtime to scene ready: {result['runtimeSceneReadySeconds']:.4f} s",tour["description"],"",
        "| Segment | Frames / camera renders | Wall P50/P95/P99 ms | >50/>100 ms | GPU P95 ms | GC0/1/2 | Bytes/frame |",
        "|---|---:|---|---|---:|---|---:|"]
    for phase in phases:
        wall=phase["wall"]
        lines.append(f"| {phase['name']} | {phase['frames']} / {phase['renderedCameraFrames']} | {' / '.join(number(wall[f'p{p}']) for p in (50,95,99))} | {wall['over50']} / {wall['over100']} | {number(phase['gpu']['p95'])} | {' / '.join(map(str,phase['gc']))} | {phase['allocatedBytes']/phase['frames']:.1f} |")
    (folder/"summary.md").write_text("\n".join(lines)+"\n",encoding="utf8")
    print("\n".join(lines))

def self_test():
    assert valid("NA") is None and valid("nan") is None and valid("0") is None
    assert timings(["NA",0,2,4,40,60,120])=={"valid":5,"p50":40.0,"p95":120.0,"p99":120.0,"over50":2,"over100":1}
    assert timings(["NA",0])["p95"] is None
    assert Path("voyage.tour.json").with_suffix("").with_suffix(".csv").name=="voyage.csv"
    with tempfile.TemporaryDirectory() as directory:
        folder=Path(directory)
        phases=[{"segment":n,"name":f"phase-{n}","travelledMetres":50 if n<=2 else 10,"riflePresented":n>2 and n%2==0,
                 "continuous":n<=2,"start":{"x":(n-1)*50,"z":0},"end":{"x":n*50,"z":0},"target":{"x":n*50,"z":0}} for n in range(1,19)]
        (folder/"fixture.tour.json").write_text(json.dumps({"phases":phases,"version":"fixture","description":"fixture","runtimeSceneReadySeconds":1,"width":1920,"height":1080}),encoding="utf8")
        columns="segment_id frame seconds state_flags wall_ms cpu_frame_ms cpu_main_ms cpu_render_ms gpu_ms main_thread_allocated_bytes gc0 gc1 gc2 x z world_camera_frame".split()
        fixture=[]
        for segment in range(19):
            for frame in range(32):
                row={column:"0" for column in columns};row.update(segment_id=str(segment),frame=str(frame+100),seconds=str(frame*10/32),world_camera_frame=str(frame+99),state_flags="2" if segment>2 and segment%2==0 else "0",wall_ms="999" if segment==0 else "20",gpu_ms="NA")
                if 1<=segment<=2:row["x"]=str((segment-1)*50+frame*50/32)
                fixture.append(row)
        def write_rows():
            with (folder/"fixture.csv").open("w",newline="",encoding="utf8") as stream:
                writer=csv.DictWriter(stream,columns);writer.writeheader();writer.writerows(fixture)
        def rejected(message):
            write_rows()
            try:
                with contextlib.redirect_stdout(io.StringIO()):analyze(folder)
            except AssertionError as error:
                assert message in str(error)
            else:
                raise AssertionError("Invalid evidence must be rejected: "+message)
        write_rows()
        with contextlib.redirect_stdout(io.StringIO()):analyze(folder)
        summary=json.loads((folder/"summary.json").read_text(encoding="utf8"))
        assert summary["measuredFrames"]==576 and summary["wall"]["p99"]==20 and summary["gpu"]["p95"] is None
        assert summary["continuousWall"]["valid"]==64 and summary["steadyWall"]["valid"]==512
        fixture[32]["world_camera_frame"]=fixture[33]["world_camera_frame"]="-1000"
        rejected("Primary camera")
        for frame in range(32):
            fixture[32+frame]["world_camera_frame"]=str(100+frame-frame%3)
        rejected("reused")
        for frame in range(32):fixture[32+frame]["world_camera_frame"]=str(frame+99)
        fixture[40]["x"]="100"
        rejected("position jump")
        fixture[40]["x"]=str(8*50/32)
        phases[1]["start"]["x"]=80
        (folder/"fixture.tour.json").write_text(json.dumps({"phases":phases,"version":"fixture","description":"fixture","runtimeSceneReadySeconds":1,"width":1920,"height":1080}),encoding="utf8")
        rejected("transition teleport")
    print("Performance analysis self-checks passed")

if __name__=="__main__":
    parser=argparse.ArgumentParser();parser.add_argument("folder",nargs="?");parser.add_argument("--self-test",action="store_true")
    args=parser.parse_args()
    if args.self_test:self_test()
    elif args.folder:analyze(args.folder)
    else:parser.error("Provide a fixed-tour output folder or --self-test")
