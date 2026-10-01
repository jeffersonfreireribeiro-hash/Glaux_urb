$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$shp=[IO.File]::ReadAllText((Join-Path $root 'src/Urb/StreetShpWriter.cs'))
$source=[IO.File]::ReadAllText((Join-Path $root 'src/Urb/StreetGisWriter.cs'))+"`n"+($shp -replace '(?m)^using .+;\r?\n','')
Add-Type -TypeDefinition $source
$temp=Join-Path ([IO.Path]::GetTempPath()) ('glaux_shp_'+[guid]::NewGuid().ToString('N'))
try {
  [IO.Directory]::CreateDirectory($temp)|Out-Null
  $p=[Buraqueira_Urb.StreetGisPackage]::new();$p.Epsg=31985;$p.CrsDefinition='PROJCS["Fixture",UNIT["metre",1]]';$p.WidthUnit='m'
  foreach($i in @(0,1)) {
    $s=[Buraqueira_Urb.StreetGisRecord]::new();$s.StreetID='street_A';$s.FeatureID="feature_$i";$s.StreetName='Rua São João';$s.StreetType='Two Way';$s.Scenario='EXISTING';$s.Status='MATCHED'
    $points=[System.Collections.Generic.List[double[]]]::new();$points.Add([double[]]@(100.0,200.0,1.0));$points.Add([double[]]@(110.0,200.0,2.0));$s.Centerline=[Buraqueira_Urb.StreetGisWriter]::Geometry($points,31985,$false)
    foreach($j in @(0,1,2)) {$e=[Buraqueira_Urb.StreetGisElementRecord]::new();$e.ElementOrder=$j;$e.ElementID="e$j";$e.ElementType='Lane';$e.MinWidth=2.5;$e.MaxWidth=3.5;$s.Elements.Add($e)}
    $p.Streets.Add($s)
  }
  $surface=[Buraqueira_Urb.StreetGisSurfaceRecord]::new();$surface.StreetID='street_A';$surface.ElementID='e1';$surface.ElementOrder=1;$surface.ElementType='Lane';$surface.Scenario='PLANNING';$ring=[System.Collections.Generic.List[double[]]]::new();$ring.Add([double[]]@(100.0,200.0,1.0));$ring.Add([double[]]@(110.0,200.0,2.0));$ring.Add([double[]]@(110.0,203.0,2.0));$ring.Add([double[]]@(100.0,203.0,1.0));$surface.Polygon=[Buraqueira_Urb.StreetGisWriter]::Geometry($ring,31985,$true);$p.Surfaces.Add($surface)
  $target=Join-Path $temp 'export.shp';$folder=[Buraqueira_Urb.StreetShpWriter]::Write($p,$target,$false)
  $python=@'
import csv,struct,sys,pathlib
d=pathlib.Path(sys.argv[1]); assert d.is_dir()
for name,count,shape in [('street_centerlines',2,13),('street_element_surfaces',1,15)]:
    shp=(d/(name+'.shp')).read_bytes(); shx=(d/(name+'.shx')).read_bytes()
    assert struct.unpack('>i',shp[:4])[0]==9994
    assert struct.unpack('<i',shp[32:36])[0]==shape
    assert struct.unpack('>i',shp[24:28])[0]*2==len(shp)
    assert struct.unpack('>i',shx[24:28])[0]*2==len(shx)
    assert (len(shx)-100)//8==count
    assert struct.unpack('<i',shp[108:112])[0]==shape
    dbf=(d/(name+'.dbf')).read_bytes()
    assert struct.unpack('<i',dbf[4:8])[0]==count
    assert (d/(name+'.prj')).exists() and (d/(name+'.cpg')).read_text()=='UTF-8'
with (d/'streets.csv').open(encoding='utf-8',newline='') as f: assert len(list(csv.DictReader(f)))==1
with (d/'street_profile_elements.csv').open(encoding='utf-8',newline='') as f: assert [int(r['ElementOrder']) for r in csv.DictReader(f)]==[0,1,2]
print('PASS: SHP/SHX/DBF/PRJ/CPG, Z geometry, CSV relations and Unicode')
'@
  python -c $python $folder
  if($LASTEXITCODE -ne 0){throw 'SHP validation failed'}
  try {[Buraqueira_Urb.StreetShpWriter]::Write($p,$target,$false)|Out-Null;throw 'overwrite guard failed'} catch [System.IO.IOException] {}
  [Buraqueira_Urb.StreetShpWriter]::Write($p,$target,$true)|Out-Null
  Write-Output 'PASS: SHP overwrite guard and replacement'
} finally {if([IO.Directory]::Exists($temp)){[IO.Directory]::Delete($temp,$true)}}
