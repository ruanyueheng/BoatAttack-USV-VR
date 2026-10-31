using UnityEngine;

namespace BoatAttack.Fishing
{
    internal static class FishingHomeHabitatArt
    {
        private static readonly Color Cedar=new Color(.52f,.34f,.20f), Cream=new Color(.87f,.82f,.66f), Teal=new Color(.18f,.38f,.36f), Sand=new Color(.71f,.64f,.43f), Stone=new Color(.44f,.48f,.43f);

        public static Mesh Build(LandAnimalKind kind,int level)
        {
            var b=new FishingLifeMesh();
            bool pond=kind==LandAnimalKind.SeaTurtle;
            b.Box(new Vector3(0,.025f,.9f),new Vector3(3.1f,.1f,3.4f),pond?Sand:new Color(.38f,.46f,.27f));
            foreach(float x in new[]{-1.5f,1.5f})foreach(float z in new[]{-.75f,.9f,2.55f})
            {
                b.Box(new Vector3(x,.53f,z),new Vector3(.12f,.98f,.12f),Cedar);
                b.Box(new Vector3(x,1.04f,z),new Vector3(.17f,.07f,.17f),Cream);
            }
            foreach(float y in new[]{.32f,.73f})
            {
                foreach(float x in new[]{-1.5f,1.5f})b.Box(new Vector3(x,y,.9f),new Vector3(.08f,.09f,3.3f),Cedar);
                foreach(float z in new[]{-.75f,2.55f})b.Box(new Vector3(0,y,z),new Vector3(3,.09f,.08f),Cedar);
            }
            for(int i=0;i<13;i++)foreach(float z in new[]{-.75f,2.55f})b.Box(new Vector3((i-6)*.23f,.51f,z),new Vector3(.045f,.68f,.055f),Cream);
            for(int i=0;i<14;i++)foreach(float x in new[]{-1.5f,1.5f})b.Box(new Vector3(x,.51f,-.6f+i*.23f),new Vector3(.055f,.68f,.045f),Cream);
            // A visible gate and latch explain the closed collision boundary while management stays outside.
            b.Box(new Vector3(0,.55f,2.61f),new Vector3(.9f,.07f,.05f),Teal,Quaternion.Euler(0,0,29));
            b.Box(new Vector3(.43f,.77f,2.63f),new Vector3(.12f,.055f,.055f),new Color(.76f,.57f,.25f));

            if(kind==LandAnimalKind.Rabbit || kind==LandAnimalKind.Fox)
            {
                bool fox=kind==LandAnimalKind.Fox;var wall=fox?Cedar:Cream;
                b.Box(new Vector3(0,.19f,-.1f),new Vector3(1.65f,.22f,1.12f),Cedar);
                b.Box(new Vector3(0,.63f,-.62f),new Vector3(1.65f,.72f,.09f),wall);
                foreach(float x in new[]{-.78f,.78f})b.Box(new Vector3(x,.63f,-.1f),new Vector3(.09f,.72f,1.08f),wall);
                foreach(float x in new[]{-.59f,.59f})b.Box(new Vector3(x,.63f,.43f),new Vector3(.48f,.72f,.09f),wall);
                b.Box(new Vector3(0,.94f,.43f),new Vector3(.7f,.1f,.09f),wall);
                for(int i=0;i<5;i++)
                {
                    float y=.34f+i*.13f;
                    b.Box(new Vector3(0,y,-.675f),new Vector3(1.67f,.012f,.01f),Cedar*.8f);
                    foreach(float x in new[]{-.835f,.835f})b.Box(new Vector3(x,y,-.1f),new Vector3(.01f,.012f,1.08f),Cedar*.8f);
                }
                foreach(float side in new[]{-1f,1f})b.Box(new Vector3(side*.44f,1.08f,-.1f),new Vector3(1.02f,.10f,1.38f),fox?new Color(.29f,.38f,.27f):Teal,Quaternion.Euler(0,0,-side*23));
                b.Box(new Vector3(0,.17f,.53f),new Vector3(.55f,.08f,.20f),Cedar,Quaternion.Euler(-18,0,0));
                b.Box(new Vector3(0,.315f,-.08f),new Vector3(.65f,.035f,.7f),Sand);
                for(int i=0;i<12;i++)b.Box(new Vector3((i%4-1.5f)*.13f,.34f,-.31f+(i/4)*.2f),new Vector3(.02f,.012f,.19f),Cream,Quaternion.Euler(0,i*37,0));
                if(fox)for(int i=0;i<3;i++)b.Cylinder(new Vector3(-1.12f,.12f,-.35f+i*.25f),new Vector3(-1.12f,.36f,-.35f+i*.25f),.21f,.16f,Stone,7);
            }
            else if(kind==LandAnimalKind.Gull)
            {
                foreach(float x in new[]{-.94f,.94f})foreach(float z in new[]{-.53f,.35f})b.Box(new Vector3(x,.9f,z),new Vector3(.08f,1.65f,.08f),Cedar);
                b.Box(new Vector3(0,1.76f,-.09f),new Vector3(2.15f,.09f,1.2f),Teal,Quaternion.Euler(-5,0,0));
                foreach(float y in new[]{.55f,1.16f})b.Cylinder(new Vector3(-.96f,y,-.2f),new Vector3(.96f,y,-.2f),.045f,.045f,Cedar);
                b.Box(new Vector3(-.65f,1.22f,-.2f),new Vector3(.48f,.06f,.42f),Sand);
                b.Box(new Vector3(.65f,1.22f,-.2f),new Vector3(.48f,.06f,.42f),Sand);
                for(int i=0;i<5;i++)b.Cylinder(new Vector3(-.75f,.18f+i*.16f,.5f-i*.10f),new Vector3(-.31f,.18f+i*.16f,.5f-i*.10f),.025f,.025f,Cream,6);
            }
            else
            {
                b.Box(new Vector3(0,.29f,-.33f),new Vector3(1.8f,.1f,.95f),Teal,Quaternion.Euler(-5,0,0));
                foreach(float x in new[]{-.8f,.8f})b.Box(new Vector3(x,.19f,-.36f),new Vector3(.09f,.25f,.85f),Cedar);
                if(pond)
                {
                    b.Box(new Vector3(-.25f,.09f,1.35f),new Vector3(2.3f,.035f,1.95f),new Color(.25f,.58f,.62f));
                    foreach(float x in new[]{-1.4f,.9f})b.Box(new Vector3(x,.12f,1.35f),new Vector3(.1f,.13f,2.05f),Stone);
                    foreach(float z in new[]{.36f,2.33f})b.Box(new Vector3(-.25f,.12f,z),new Vector3(2.4f,.13f,.1f),Stone);
                    for(int i=0;i<5;i++)b.Box(new Vector3(-.85f+i*.42f,.113f,.6f+(i%2)*.45f),new Vector3(.24f,.007f,.025f),new Color(.55f,.76f,.74f));
                }
                else
                {
                    b.Cylinder(new Vector3(-.95f,.075f,.02f),new Vector3(-.95f,.21f,.02f),.3f,.25f,Stone,7);
                    for(int i=0;i<3;i++)b.Cylinder(new Vector3(.87f+i*.16f,.08f,-.13f),new Vector3(.87f+i*.16f,.32f+i*.07f,-.13f),.075f,.04f,new Color(.3f,.43f,.23f),6);
                }
            }
            Bowl(b,new Vector3(1.13f,.08f,1.96f),new Color(.28f,.55f,.64f));
            Bowl(b,new Vector3(1.13f,.08f,1.36f),Sand);
            if(level>1)
            {
                b.Box(new Vector3(-1.14f,.2f,.1f),new Vector3(.3f,.12f,.75f),Sand);
                b.Cylinder(new Vector3(-1.17f,.34f,-.1f),new Vector3(-1.17f,.34f,.37f),.13f,.13f,Cedar,8);
            }
            return b.Finish("Coastal habitat / "+kind+" / "+level);
        }

        private static void Bowl(FishingLifeMesh b,Vector3 p,Color contents)
        {
            b.Cylinder(p,p+Vector3.up*.11f,.14f,.18f,Teal,12);
            b.Cylinder(p+Vector3.up*.112f,p+Vector3.up*.12f,.145f,.145f,contents,12);
        }

        public static void AddCollision(Transform root)
        {
            void Box(Vector3 center,Vector3 size){var box=root.gameObject.AddComponent<BoxCollider>();box.center=center;box.size=size;}
            Box(new Vector3(0,.025f,.9f),new Vector3(3.1f,.1f,3.4f));
            foreach(float x in new[]{-1.5f,1.5f})Box(new Vector3(x,.5f,.9f),new Vector3(.12f,.94f,3.4f));
            foreach(float z in new[]{-.75f,2.55f})Box(new Vector3(0,.5f,z),new Vector3(3.1f,.94f,.12f));
        }
    }
}
