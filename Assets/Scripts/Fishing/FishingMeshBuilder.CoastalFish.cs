using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingMeshBuilder
    {
        // Original silhouettes and vertex paint; one material and no texture sampler per fish.
        private void CoastalSpecies(FishKind kind)
        {
            bool deep = kind == FishKind.SeaBream || kind == FishKind.Butterfly;
            bool heavy = kind == FishKind.Grouper || kind == FishKind.Tuna;
            bool lion = kind == FishKind.Lionfish;
            var body = new BodyShape { Tail = -.48f, Snout = .53f, Width = deep ? .105f : heavy ? .24f : .14f,
                Height = deep ? .37f : heavy ? .26f : .17f, Peak = heavy ? 1.45f : 1.2f, Peduncle = .14f, Belly = .86f };
            var top = kind == FishKind.Mackerel ? new Color(.08f,.38f,.43f) : kind == FishKind.SeaBream ? new Color(.72f,.43f,.36f)
                : kind == FishKind.Butterfly ? new Color(.96f,.71f,.13f) : kind == FishKind.Grouper ? new Color(.40f,.44f,.25f)
                : lion ? new Color(.65f,.17f,.11f) : kind == FishKind.Lanternfish ? new Color(.07f,.14f,.29f) : new Color(.12f,.26f,.44f);
            var belly = new Color(.85f,.9f,.85f); var fin = Color.Lerp(top, new Color(.95f,.66f,.26f), .4f);
            Loft(Stations(body, 18), body, (p,t,a) =>
            {
                float up = Mathf.Sin(a);
                var color = Color.Lerp(belly,top,Mathf.Clamp01(up * .65f + .45f));
                if (kind == FishKind.Mackerel && up > .15f && Mathf.Sin(t * 70 + a * 3) > .4f) color *= .5f;
                if ((lion || kind == FishKind.Butterfly) && Mathf.Sin(t * (lion ? 45 : 22)) > .45f) color = lion ? belly : top * .23f;
                if (kind == FishKind.Grouper && Mathf.Sin(t * 95 + Mathf.Cos(a * 9) * 2) > .68f) color *= .45f;
                if (kind == FishKind.Sardine && Mathf.Abs(up) < .17f) color = new Color(.45f,.78f,.86f);
                return color;
            }, 16);
            // Forked caudal fin, with visible fin-ray insets.
            var root = new Vector3(0,0,body.Tail);
            foreach (float side in new[] { -1f, 1f })
            {
                var tip = new Vector3(0,side * (heavy ? .3f : .24f),-.84f);
                Fin(root,tip,new Vector3(0,side*.035f,-.69f),fin,top,belly);
                Tube(root,tip,.006f,.002f,Color.Lerp(fin,belly,.5f),5);
            }
            int rays = lion ? 9 : deep ? 7 : 5;
            for (int i=0; i<rays; i++)
            {
                float t=.25f+i*.055f;
                var a=body.Surface(t,Mathf.PI*.5f); var b=body.Surface(t+.055f,Mathf.PI*.5f);
                var tip=(a+b)*.5f+new Vector3(0,lion?.36f:deep?.14f:.12f,-.07f);
                Fin(a,tip,b,fin,Color.Lerp(fin,belly,.5f),fin);
                if(lion)Tube(a,tip,.007f,.002f,belly,5);
            }
            if(lion)
            {
                foreach(float side in new[]{-1f,1f}) for(int i=0;i<5;i++)
                {
                    var a=body.Surface(.67f,side>0?0:Mathf.PI);
                    var tip=a+new Vector3(side*(.20f+i*.045f),.05f-i*.055f,-.13f-i*.045f);
                    Fin(a,tip,tip+new Vector3(0,-.07f,-.06f),fin,belly,top);
                }
            }
            else Pectorals(body,.70f,heavy?.26f:.18f,fin,belly);
            Eyes(body,.84f,deep?.08f:.04f,deep?.66f:.62f);
            Ellipsoid(new Vector3(0,-.015f,body.Snout),new Vector3(.035f,.022f,.015f),top*.3f,8,4);
            if(kind==FishKind.Lanternfish) foreach(float side in new[]{-1f,1f}) for(int i=0;i<6;i++)
                Ellipsoid(body.Surface(.35f+i*.065f,side>0?-.5f:Mathf.PI+.5f),Vector3.one*.015f,new Color(.50f,.96f,.82f),6,3);
            if(kind==FishKind.Butterfly) foreach(float side in new[]{-1f,1f})
                Ellipsoid(body.Surface(.30f,side>0?.3f:Mathf.PI-.3f),new Vector3(.012f,.07f,.06f),Pupil,8,5);
        }
    }
}
