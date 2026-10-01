$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $root 'src/Urb/StreetGisWriter.cs')))
$file=Join-Path ([IO.Path]::GetTempPath()) ('glaux_gis_'+[guid]::NewGuid().ToString('N')+'.gpkg')
try {
  $p=[Buraqueira_Urb.StreetGisPackage]::new();$p.Epsg=31985;$p.CrsDefinition='PROJCS["Fixture",UNIT["metre",1]]';$p.WidthUnit='m'
  $counts=@(1,3,7,11)
  for($i=0;$i -lt $counts.Count;$i++) {
    foreach($segment in @(1,2)) {
      $s=[Buraqueira_Urb.StreetGisRecord]::new();$s.StreetID="street_$i";$s.StreetName="Rua $i";$s.StreetType='Custom';$s.FeatureID="feature_${i}_$segment";$s.SourceID="source_$segment";$s.Scenario='EXISTING';$s.Status='MATCHED'
      $coordinates=[System.Collections.Generic.List[double[]]]::new();$coordinates.Add([double[]]@(100.0,200.0,0.0));$coordinates.Add([double[]]@(110.0,200.0,0.0));$s.Centerline=[Buraqueira_Urb.StreetGisWriter]::Geometry($coordinates,31985,$false)
      for($j=0;$j -lt $counts[$i];$j++) {
        $e=[Buraqueira_Urb.StreetGisElementRecord]::new();$e.ElementOrder=$j;$e.ElementID="element_${i}_$j";$e.ElementType= if($j % 2 -eq 0){'Sidewalk'}else{'Lane'};$e.MinWidth=1.5;$e.MaxWidth=if($j -eq 0){2.5}else{1.5};$e.IsFixed=($j -ne 0);$e.Direction=if($j % 2 -eq 0){0}else{1};$e.Required=$true;$s.Elements.Add($e)
      }
      $p.Streets.Add($s)
    }
  }
  $section=[Buraqueira_Urb.StreetGisSectionRecord]::new();$section.StreetID='street_0';$section.SectionID='T0_0';$section.StationIndex=0;$section.AvailableWidth=2;$section.Status='FITTED';$actual=[Buraqueira_Urb.StreetGisElementRecord]::new();$actual.ElementOrder=0;$actual.ElementID='element_0_0';$actual.MaxWidth=2;$actual.Status='ACTIVE';$section.Elements.Add($actual);$p.Sections.Add($section)
  $surface=[Buraqueira_Urb.StreetGisSurfaceRecord]::new();$surface.StreetID='street_0';$surface.RunID='0';$surface.ElementOrder=0;$surface.ElementID='element_0_0';$surface.ElementType='Sidewalk';$surface.Status='GENERATED';$surface.Scenario='PLANNING';$ring=[System.Collections.Generic.List[double[]]]::new();$ring.Add([double[]]@(100.0,200.0,0.0));$ring.Add([double[]]@(110.0,200.0,0.0));$ring.Add([double[]]@(110.0,202.0,0.0));$ring.Add([double[]]@(100.0,202.0,0.0));$surface.Polygon=[Buraqueira_Urb.StreetGisWriter]::Geometry($ring,31985,$true);$p.Surfaces.Add($surface)
  $written=[Buraqueira_Urb.StreetGisWriter]::Write($p,$file,$false)
  if(-not [IO.File]::Exists($written)){throw 'missing file'}
  $python=@'
import sqlite3,sys
c=sqlite3.connect(sys.argv[1])
assert c.execute('pragma application_id').fetchone()[0]==1196444487
assert c.execute('select count(*) from streets').fetchone()[0]==4
assert c.execute('select count(*) from street_centerlines').fetchone()[0]==8
assert c.execute('select count(*) from street_profile_elements').fetchone()[0]==22
assert c.execute('select count(*) from street_sections').fetchone()[0]==1
assert c.execute('select count(*) from street_section_elements').fetchone()[0]==1
assert c.execute('select count(*) from street_element_surfaces').fetchone()[0]==1
assert c.execute("select ElementID,ElementOrder,ElementType from street_element_surfaces").fetchone()==('element_0_0',0,'Sidewalk')
assert c.execute("select srs_id from gpkg_geometry_columns where table_name='street_centerlines'").fetchone()[0]==31985
assert c.execute('select count(*) from pragma_foreign_key_check').fetchone()[0]==0
assert c.execute("select ElementOrder,MinWidth,MaxWidth,IsFixed from street_profile_elements where StreetID='street_0'").fetchone()==(0,1.5,2.5,0)
print('PASS: GPKG schema, 1/3/7/11 elements, 2 segments, CRS, sections')
'@
  python -c $python $file
  if($LASTEXITCODE -ne 0){throw 'roundtrip failed'}
  try {[Buraqueira_Urb.StreetGisWriter]::Write($p,$file,$false)|Out-Null;throw 'overwrite guard failed'} catch [System.IO.IOException] {}
  [Buraqueira_Urb.StreetGisWriter]::Write($p,$file,$true)|Out-Null
  Write-Output 'PASS: overwrite guard and explicit replacement'
} finally { if([IO.File]::Exists($file)){[IO.File]::Delete($file)} }
