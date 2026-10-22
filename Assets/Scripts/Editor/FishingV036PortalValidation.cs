using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV036PortalValidation
    {
        public static void Run()=>FishingV011Validation.Begin("V036-PORTAL");
        private static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException(label);Debug.Log("[V036-PORTAL] CHECK: "+label);}
        public static IEnumerator Checks(FishingPrototype c)
        {
            for(int id=0;id<4;id++)
            {
                Inspect(c.Realms.Portal(id));Inspect(c.Realms.ExitPortal(id));
                Check(Vector3.Distance(c.Realms.ExitPortal(id).position,c.Realms.ExitPortal(id).parent.position)>10,"Exit remains at the terminal platform "+id);
            }
            var levels=FishingV035Levels.Checks(c);try{while(levels.MoveNext())yield return levels.Current;}finally{(levels as IDisposable)?.Dispose();}
        }
        private static void Inspect(Transform target)
        {
            var visual=target.GetComponentInChildren<FishingPortalVisual>(true);
            Check(visual!=null && visual.Height>2,"Celestial portal is visibly framed: "+target.parent.name+" / "+target.name);
            Check(visual.GetComponentsInChildren<MeshRenderer>(true).Length==3,"Portal uses three batched surfaces");
            Check(visual.GetComponentsInChildren<Collider>(true).Length==0 && visual.GetComponentsInChildren<ParticleSystem>(true).Length==0,"Light curtain never blocks walking or simulates particles");
            var curtain=visual.transform.Find("Visible flowing light curtain").GetComponent<MeshRenderer>();
            Check(curtain.sharedMaterial.shader.name=="BoatAttack/FishingPortal" && curtain.sharedMaterial.renderQueue>=3000,"Portal has its translucent animated curtain");
        }
        public static void CheckEntry(FishingPrototype c,int id)
        {
            var exit=c.Realms.ExitPortal(id);Inspect(exit);
            Check(Vector3.Distance(exit.position,c.Shore.Position)>12,"No return portal at the sanctuary entrance "+id);
            Check(!c.Realms.Exit() && !c.Realms.Transitioning,"Entry cannot accidentally trigger a return "+id);
            Check(c.Shore.Ground(exit.position,out var floor) && Mathf.Abs(floor.y-exit.position.y)<.25f,"Terminal portal stands on the authored floor "+id);
        }
        public static void CheckTerminal(FishingPrototype c,int id)
        {
            Check(c.Realms.TryTarget(out int selected,out var target) && selected==4 && target==c.Realms.ExitPortal(id),"Terminal portal can be used independently of claiming the echo "+id);
            Check(c.Realms.NearbyHint.Contains("归航门"),"Terminal prompt clearly says where it leads "+id);
        }
    }
}
