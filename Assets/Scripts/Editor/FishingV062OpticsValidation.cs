using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BoatAttack.Fishing;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    public static class FishingV062OpticsValidation
    {
        private static void Check(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException(message);
            Debug.Log("[V062-OPTICS] CHECK: "+message);
        }

        public static void Checks(FishingPrototype c)
        {
            Check(c.HasIsolatedProfile,"Photo checks require an isolated save profile");
            string root=Path.Combine(Application.temporaryCachePath,"v062-photo-import-"+Guid.NewGuid().ToString("N"));
            string source=Path.Combine(root,"external"),destination=Path.Combine(root,"internal");
            Directory.CreateDirectory(source);
            string first=FishingPhotoCodex.FileName(0,0,-1),second=FishingPhotoCodex.FileName(1,0,-1);
            try
            {
                File.WriteAllBytes(Path.Combine(source,first),new byte[]{1,2,3});
                File.WriteAllBytes(Path.Combine(source,second),new byte[]{4,5,6});
                File.WriteAllText(Path.Combine(source,"not-a-photo.txt"),"Ignore unrelated files.");
                Directory.CreateDirectory(destination);
                File.WriteAllBytes(Path.Combine(destination,second),new byte[]{7,8,9});
                Check(FishingLifeWorld.ImportLegacyAlbum(source,destination),"Old external album imports successfully");
                Check(File.ReadAllBytes(Path.Combine(destination,first)).SequenceEqual(new byte[]{1,2,3}),"Imported photo bytes are unchanged");
                Check(File.ReadAllBytes(Path.Combine(destination,second)).SequenceEqual(new byte[]{7,8,9}),"Import never overwrites a newer internal photo");
                Check(FishingLifeWorld.AlbumFiles(source).Count==2 && !File.Exists(Path.Combine(destination,"not-a-photo.txt")),"Original photos remain and unrelated files are excluded");
                File.Delete(Path.Combine(destination,first));
                Check(FishingLifeWorld.ImportLegacyAlbum(source,destination) && !File.Exists(Path.Combine(destination,first)),"Completed import does not resurrect photos removed by album rotation");

                string blocked=Path.Combine(root,"blocked");File.WriteAllText(blocked,"Not a directory.");
                Check(!FishingLifeWorld.ImportLegacyAlbum(source,blocked) && FishingLifeWorld.AlbumFiles(source).Count==2,"Unwritable destination preserves all old photos and reports failure");

                string partial=Path.Combine(root,"partial");Directory.CreateDirectory(Path.Combine(partial,first));
                Check(!FishingLifeWorld.ImportLegacyAlbum(source,partial),"Partial import is reported instead of marking the album complete");
                Check(File.Exists(Path.Combine(partial,second)) && !File.Exists(Path.Combine(partial,".external-imported")),"Readable photos survive a partial import and it remains retryable");
                Check(!Directory.GetFiles(partial,"*.tmp").Any() && FishingLifeWorld.AlbumFiles(source).Count==2,"Failed import leaves no partial JPG and retains original files");
                Directory.Delete(Path.Combine(partial,first));
                Check(FishingLifeWorld.ImportLegacyAlbum(source,partial) && FishingLifeWorld.AlbumFiles(partial).Count==2,"A later session can complete an interrupted import");
                Check(FishingLifeWorld.ImportLegacyAlbum(source,source),"Identical isolated paths need no migration");
                Check(FishingLifeWorld.ImportLegacyAlbum(Path.Combine(root,"missing"),Path.Combine(root,"unused")) && !Directory.Exists(Path.Combine(root,"unused")),"A fresh game does not manufacture an external album");
            }
            finally{Directory.Delete(root,true);}

            var run=typeof(FishingPrototype).GetField("_runId",BindingFlags.Instance|BindingFlags.NonPublic);
            string original=(string)run.GetValue(c),albumA=null,albumB=null;
            var image=new Texture2D(2,2,TextureFormat.RGB24,false);
            try
            {
                run.SetValue(c,"v062-album-a-"+Guid.NewGuid().ToString("N"));albumA=c.AlbumDirectory;
                Directory.CreateDirectory(albumA);File.WriteAllBytes(Path.Combine(albumA,first),image.EncodeToJPG());
                Check(c.Life.Album.Count==1,"First isolated voyage loads its own album");
                run.SetValue(c,"v062-album-b-"+Guid.NewGuid().ToString("N"));albumB=c.AlbumDirectory;
                Check(c.Life.Album.Count==0,"Changing voyage invalidates album textures without showing the prior voyage's photos");
            }
            finally
            {
                run.SetValue(c,original);c.Life.RefreshAlbum();UnityEngine.Object.Destroy(image);
                if(albumA!=null && Directory.Exists(albumA))Directory.Delete(albumA,true);
                if(albumB!=null && Directory.Exists(albumB))Directory.Delete(albumB,true);
            }
        }
    }
}
