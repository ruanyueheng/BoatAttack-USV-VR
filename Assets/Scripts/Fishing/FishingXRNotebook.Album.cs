using UnityEngine;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingXRNotebook
    {
        public const int AlbumPage=25,PhotoCodexPage=26;
        private RawImage _albumPreview;
        private int _albumSelected=-1;

        private void BuildAlbumPreview()
        {
            var go=new GameObject("Selected album photograph",typeof(RectTransform),typeof(RawImage));
            go.layer=5;go.transform.SetParent(_canvas.transform,false);
            _albumPreview=go.GetComponent<RawImage>();_albumPreview.raycastTarget=false;_albumPreview.material=_homeMaterial;
            HomeRect(_albumPreview.rectTransform,new Rect(62,401,335,335f*FishingLifeWorld.PhotoHeight/FishingLifeWorld.PhotoWidth));
            go.SetActive(false);
        }

        private void RefreshAlbumPage()
        {
            var album=_c.Life.Album;
            _offset=Mathf.Clamp(_offset,0,Mathf.Max(0,(album.Count-1)/6)*6);
            if(_albumSelected<0 || _albumSelected>=album.Count)_albumSelected=album.Count-1;
            _status.text="相册 · "+album.Count+" / "+FishingLifeWorld.AlbumLimit+" 张\n较新的照片排在前面\n点选右侧照片，在左页放大查看。\n";
            if(_albumSelected>=0)
            {
                var selected=album[_albumSelected];
                _status.text+="\n第 "+(selected.day+1)+" 天\n"+(selected.entry>=0?FishingPhotoCodex.Name(selected.entry):"海湾留影");
            }
            else _status.text+="\n还没有照片。\n举起相机，扣右扳机拍下第一张。";
            for(int i=0;i<6;i++)
            {
                int index=album.Count-1-_offset-i;if(index<0)break;
                var photo=album[index];
                Set(i,"第 "+(photo.day+1)+" 天\n"+(photo.entry>=0?FishingPhotoCodex.Name(photo.entry):"海湾留影"),()=>{_albumSelected=index;Refresh();});
                Photo(i,photo.tex);
            }
            Pager(album.Count);Set(7,"照片图鉴",()=>Open(PhotoCodexPage));
        }

        private void RefreshPhotoCodexPage()
        {
            int photographed=0;for(int e=0;e<FishingPhotoCodex.Count;e++)if(FishingPhotoCodex.Photographed(_c.Career,e))photographed++;
            _status.text="照片图鉴 · "+photographed+" / "+FishingPhotoCodex.Count+"\n与桌面相册共用拍摄记录。\n★ 已拍下插图\n· 已见，尚未拍下\n？ 尚未见到\n\n抬手举起相机，扣右扳机拍照。";
            for(int i=0;i<6;i++)
            {
                int entry=_offset+i;if(entry>=FishingPhotoCodex.Count)break;
                bool shot=FishingPhotoCodex.Photographed(_c.Career,entry),seen=FishingPhotoCodex.Seen(_c.Career,entry);
                Set(i,(shot?"★ ":seen?"· ":"？ ")+(seen?FishingPhotoCodex.Name(entry):"未见条目")+"\n"+(shot?"已拍下插图":FishingPhotoCodex.Hint(entry)),null);
            }
            Pager(FishingPhotoCodex.Count);
        }

        private void PresentAlbumPage()
        {
            bool album=_page==AlbumPage;
            _albumPreview.gameObject.SetActive(album && _albumSelected>=0 && _albumSelected<_c.Life.Album.Count);
            if(!album)return;
            HomeRect(_status.rectTransform,new Rect(62,191,335,200));_status.fontSize=21;
            if(_albumPreview.gameObject.activeSelf)_albumPreview.texture=_c.Life.Album[_albumSelected].tex;
            for(int i=0;i<6;i++)
            {
                if(!_icons[i].gameObject.activeSelf)continue;
                HomeRect(_icons[i].rectTransform,new Rect(10,22,160,90));
                HomeRect(_labels[i].rectTransform,new Rect(181,18,108,40));_labels[i].fontSize=20;
                HomeRect(_homeCaptions[i].rectTransform,new Rect(181,60,108,75));_homeCaptions[i].fontSize=20;
                _homeArrows[i].gameObject.SetActive(false);
            }
        }
    }
}
