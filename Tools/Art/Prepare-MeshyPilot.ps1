param([string]$IntakeDirectory = 'Artifacts/MeshyPilotIntake/58acdcc19240')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not [IO.Path]::IsPathRooted($IntakeDirectory)) { $IntakeDirectory = Join-Path $repo $IntakeDirectory }
$source = Join-Path $IntakeDirectory 'Meshy_AI_hasan_agha_rigged_biped'
$destination = Join-Path $repo 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot/Source'
$fbx = Join-Path $source 'Meshy_AI_hasan_agha_rigged_biped_Animation_all_frame_rate_60.fbx'
if ((Get-FileHash -LiteralPath $fbx -Algorithm SHA256).Hash.ToLowerInvariant() -ne '0ee0076119289b4748fa0eeb11fa631d9a03e55b9b1208db6afe19beba410b27') { throw 'Unexpected Meshy source FBX hash.' }
$expectedMaps=@{
    'Meshy_AI_hasan_agha_rigged_biped_texture_0.png'='9f9db1fdf8a3119b140cba87df8477f62a9725ab8a83ddace8f1a2c2a54ba563'
    'Meshy_AI_hasan_agha_rigged_biped_texture_0_metallic.png'='70a7df9313df05901a4734241889054a7fc267855e1b8bf61a122b3b4a177490'
    'Meshy_AI_hasan_agha_rigged_biped_texture_0_roughness.png'='bfe86bdcf518eaefbb55439f7e95969e65b6085dde88210f6a861922f8d341b7'
}
foreach($entry in $expectedMaps.GetEnumerator()) {
    if((Get-FileHash -LiteralPath (Join-Path $source $entry.Key) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.Value){throw ('Unexpected Meshy texture hash: '+$entry.Key)}
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $fbx -Destination (Join-Path $destination 'Hasan_Meshy.fbx')
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Private.Windows.GdiPlus,System.Private.Windows.Core,System.Runtime.InteropServices -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class MeshyTexturePacking {
    public static Bitmap Resize(string file,int size) {
        using(var input=Image.FromFile(file)) {
            var output=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(output)) {
                g.CompositingMode=CompositingMode.SourceCopy;
                g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.DrawImage(input,new Rectangle(0,0,size,size));
            }
            return output;
        }
    }
    public static void Pack(string metal,string rough,string output,int size) {
        using(var m=Resize(metal,size)) using(var r=Resize(rough,size)) using(var o=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
            var area=new Rectangle(0,0,size,size);
            var md=m.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var rd=r.LockBits(area,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
            var od=o.LockBits(area,ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try {
                var ma=new byte[md.Stride*size]; var ra=new byte[rd.Stride*size]; var oa=new byte[od.Stride*size];
                Marshal.Copy(md.Scan0,ma,0,ma.Length); Marshal.Copy(rd.Scan0,ra,0,ra.Length);
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                    int i=y*od.Stride+x*4; byte metallic=ma[y*md.Stride+x*4+2];
                    oa[i]=metallic; oa[i+1]=metallic; oa[i+2]=metallic; oa[i+3]=(byte)(255-ra[y*rd.Stride+x*4+2]);
                }
                Marshal.Copy(oa,0,od.Scan0,oa.Length);
            } finally {m.UnlockBits(md);r.UnlockBits(rd);o.UnlockBits(od);}
            o.Save(output,ImageFormat.Png);
        }
    }
}
'@
$texturePrefix = Join-Path $source 'Meshy_AI_hasan_agha_rigged_biped_texture_0'
$base = [MeshyTexturePacking]::Resize(($texturePrefix + '.png'),2048)
try { $base.Save((Join-Path $destination 'BaseColor.png'),[Drawing.Imaging.ImageFormat]::Png) } finally { $base.Dispose() }
[MeshyTexturePacking]::Pack(($texturePrefix + '_metallic.png'),($texturePrefix + '_roughness.png'),(Join-Path $destination 'MetallicSmoothness.png'),2048)
Write-Output 'MESHY_TEXTURES_PREPARED BaseColor=2048 sRGB; MetallicSmoothness=2048 linear RGB=metallic A=1-roughness. Original archive unchanged.'
