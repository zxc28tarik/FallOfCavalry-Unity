using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Original deterministic tiling surface studies; not historical source imagery.</summary>
    public static class HistoricalArtSurfaceAuthoring
    {
        public const int Size=256;
        public static Texture2D Albedo(string category)
        {
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,true){name="TEX_"+category+"_Weave",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var pixels=new Color[Size*Size];
            for(var y=0;y<Size;y++)for(var x=0;x<Size;x++)
            {
                var h=Height(category,x,y);var value=category=="Mail"?.40f+h*.60f:category=="Wood"?.64f+h*.34f:category=="Hair"?.65f+h*.32f:.89f+h*.10f;
                pixels[y*Size+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        public static Texture2D Normal(string category)
        {
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,true,true){name="TEX_"+category+"_SurfaceN",wrapMode=TextureWrapMode.Repeat,anisoLevel=4};
            var pixels=new Color[Size*Size];
            for(var y=0;y<Size;y++)for(var x=0;x<Size;x++)
            {
                var n=new Vector3((Height(category,x-1,y)-Height(category,x+1,y))*3,(Height(category,x,y-1)-Height(category,x,y+1))*3,1).normalized;
                // Unity Standard UnpackNormalmapRGorAG: x = red * alpha, y = green.
                pixels[y*Size+x]=new Color(1,n.y*.5f+.5f,1,n.x*.5f+.5f);
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        public static float Height(string category,int x,int y)
        {
            x=(x%Size+Size)%Size;y=(y%Size+Size)%Size;
            if(category=="Mail")
            {
                // Staggered linked ovals; the holes are recesses, not a shiny checkerboard.
                var row=y/32;var u=((x+(row%2)*16)%32-16)/12f;var v=(y%32-16)/13f;
                return Mathf.Exp(-Mathf.Pow((Mathf.Sqrt(u*u+v*v)-.80f)*9,2));
            }
            var grain=Hash(x,y);
            if(category=="Wood")return .5f+.3f*Mathf.Sin(x*.15f+2*Mathf.Sin(y*.025f))+.12f*grain;
            if(category=="Hair")return .5f+.25f*Mathf.Sin(x*2.1f+Mathf.Sin(y*.03f))+.12f*grain;
            if(category=="Skin"||category=="Leather")return .3f+grain*.55f;
            if(category=="Steel")return .5f+grain*.08f;
            return .5f+.18f*Mathf.Sin(x*Mathf.PI*.5f)*Mathf.Sin(y*Mathf.PI*.5f)+grain*.08f;
        }
        private static float Hash(int x,int y){unchecked{var h=(uint)(x*374761393+y*668265263);h=(h^(h>>13))*1274126177u;return (h&65535)/65535f;}}
    }
}
