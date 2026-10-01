using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Buraqueira_Urb
{
    // GH owns each variable input, its identity and its wires. Semantic metadata is
    // stored in that input's native serialized Description, never in a parallel list.
    public sealed class StreetProfileDefinitionDynamic_Component : GH_Component, IGH_VariableParameterComponent
    {
        private sealed class Spec
        { public string Kind = "Road"; public int Direction; public bool Required = true; }
        private const string MetadataPrefix = "GLAUX_STREET_ELEMENT_V1:";
        private static readonly string[] Kinds = { "Road", "Lane", "Sidewalk", "Median", "Pedestrian",
            "Cycle Track", "Parking", "Tree Strip", "Green Strip", "Furniture Strip",
            "Transit", "Shoulder", "Other" };

        public StreetProfileDefinitionDynamic_Component() : base(
            "Street Profile Definition", "StreetProfile",
            "Compõe faixas semânticas da esquerda para a direita. Use +/− nativos; menu do componente altera tipo, direção, ordem e obrigatoriedade.",
            "Glaux Urb", "01 | Infraestrutura Viária") { }
        public override Guid ComponentGuid => new Guid("03e7734c-626c-46c3-9790-5daf9f219971");
        protected override Bitmap Icon => GlauxUrbIcons.StreetProfileDefinition;

        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddTextParameter("Street", "Street", "Identidade da via; para associação automática ao GIS use o valor SourceStreetID de SectionMeta.", GH_ParamAccess.item, "Rua");
            p.AddTextParameter("Street Type", "StreetType", "Classificação da via inteira; cada faixa tem seu próprio Element Type.", GH_ParamAccess.item, "Custom");
            p.AddParameter(MakeParam());
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("Street Profile", "Profile", "Objeto semântico com Street, Type e Elements[] ordenados.", GH_ParamAccess.item);
            p.AddTextParameter("Profile Elements", "Elements", "Elementos e IDs na ordem esquerda → direita.", GH_ParamAccess.list);
            p.AddTextParameter("Profile Preview", "Preview", "Resumo compacto da composição.", GH_ParamAccess.item);
        }

        private static Param_GenericObject MakeParam(Spec spec = null)
        {
            var param = new Param_GenericObject { Access = GH_ParamAccess.item, Optional = true };
            WriteSpec(param, spec ?? new Spec());
            return param;
        }
        private static Spec ReadSpec(IGH_Param param)
        {
            string description = param.Description ?? "";
            int marker = description.LastIndexOf(MetadataPrefix, StringComparison.Ordinal);
            if (marker >= 0)
            {
                string[] fields = description.Substring(marker + MetadataPrefix.Length).Split('|');
                if (fields.Length == 3 && int.TryParse(fields[0], out int kindIndex) &&
                    kindIndex >= 0 && kindIndex < Kinds.Length &&
                    int.TryParse(fields[1], out int direction) && direction >= -1 && direction <= 1 &&
                    (fields[2] == "0" || fields[2] == "1"))
                    return new Spec { Kind = Kinds[kindIndex], Direction = direction, Required = fields[2] == "1" };
            }
            // Older GH definitions may have no embedded metadata. Preserve the visible
            // type/direction and migrate their old component chunks during Read().
            string kind = Kinds.FirstOrDefault(x => string.Equals(x, param.Name, StringComparison.OrdinalIgnoreCase)) ?? "Road";
            return new Spec { Kind = kind, Direction = (param.NickName ?? "").Contains("←") ? -1 :
                (param.NickName ?? "").Contains("→") ? 1 : 0, Required = true };
        }
        private static void WriteSpec(IGH_Param param, Spec spec)
        {
            string shortName = spec.Kind == "Tree Strip" ? "Tree" :
                spec.Kind == "Furniture Strip" ? "Furniture" : spec.Kind;
            param.Name = spec.Kind;
            param.NickName = shortName + (spec.Direction > 0 ? " →" : spec.Direction < 0 ? " ←" : "");
            param.Description = "Faixa semântica; Width Domain mínimo|máximo (ex.: 2.5|3.5), intervalo GH ou número fixo. " +
                "ElementID=" + param.InstanceGuid + "\n" + MetadataPrefix +
                Array.IndexOf(Kinds, spec.Kind).ToString(CultureInfo.InvariantCulture) + "|" +
                spec.Direction.ToString(CultureInfo.InvariantCulture) + "|" + (spec.Required ? "1" : "0");
            param.Optional = true;
        }
        public bool CanInsertParameter(GH_ParameterSide side, int index)
            => side == GH_ParameterSide.Input && index >= 2 && index <= Params.Input.Count;
        public bool CanRemoveParameter(GH_ParameterSide side, int index)
            => side == GH_ParameterSide.Input && index >= 2 && index < Params.Input.Count && Params.Input.Count > 3;
        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            if (!CanInsertParameter(side,index)) return null;
            return MakeParam();
        }
        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            if (!CanRemoveParameter(side,index)) return false;
            return true;
        }
        public void VariableParameterMaintenance()
        {
            for (int i=2;i<Params.Input.Count;i++)
            {
                var param=Params.Input[i];
                WriteSpec(param, ReadSpec(param));
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            VariableParameterMaintenance();
            return base.Write(writer);
        }
        public override bool Read(GH_IReader reader)
        {
            bool ok=base.Read(reader);
            // One-time migration of definitions saved with positional GlauxElement chunks.
            if(reader.ItemExists("GlauxElementCount"))
            {
                int count=Math.Min(reader.GetInt32("GlauxElementCount"),Params.Input.Count-2);
                for(int i=0;i<count;i++)
                {
                    if(reader.ChunkExists("GlauxElement",i))
                    {
                        var chunk=reader.FindChunk("GlauxElement",i);
                        var s=new Spec { Kind=chunk.GetString("Kind"),
                            Direction=chunk.GetInt32("Direction"),Required=chunk.GetBoolean("Required") };
                        if(!Kinds.Contains(s.Kind))s.Kind="Road";
                        WriteSpec(Params.Input[i+2],s);
                    }
                }
            }
            VariableParameterMaintenance();
            return ok;
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            menu.Items.Add(new ToolStripSeparator());
            var presets=new ToolStripMenuItem("Initial profile preset")
            { Enabled=Params.Input.Count==3 && Params.Input[2].SourceCount==0 };
            AddPreset("Pedestrian",new[]{("Pedestrian",0)});
            AddPreset("Local Street",new[]{("Sidewalk",0),("Lane",1),("Lane",-1),("Sidewalk",0)});
            AddPreset("One Way",new[]{("Sidewalk",0),("Lane",1),("Sidewalk",0)});
            AddPreset("Two Way",new[]{("Sidewalk",0),("Lane",1),("Lane",-1),("Sidewalk",0)});
            AddPreset("Avenue",new[]{("Sidewalk",0),("Lane",1),("Lane",1),("Median",0),
                ("Lane",-1),("Lane",-1),("Sidewalk",0)});
            AddPreset("Shared Street",new[]{("Sidewalk",0),("Road",0),("Sidewalk",0)});
            menu.Items.Add(presets);
            for(int i=2;i<Params.Input.Count;i++)
            {
                int index=i;
                int slot=i-2;
                var parameter=Params.Input[i];
                var spec=ReadSpec(parameter);
                var group=new ToolStripMenuItem((slot+1).ToString("00")+" "+Params.Input[i].NickName);
                var types=new ToolStripMenuItem("Element type");
                foreach(var kind in Kinds)
                {
                    string chosen=kind;
                    var item=new ToolStripMenuItem(kind){Checked=spec.Kind==kind};
                    item.Click+=(sender,args)=>Change(parameter,s=>{s.Kind=chosen;
                        if(chosen=="Lane"&&s.Direction==0)s.Direction=1;
                        if(chosen=="Median"||chosen=="Tree Strip"||chosen=="Green Strip"||
                           chosen=="Furniture Strip"||chosen=="Parking")s.Required=false;});
                    types.DropDownItems.Add(item);
                }
                group.DropDownItems.Add(types);
                var directions=new ToolStripMenuItem("Direction");
                foreach(var option in new[]{("None",0),("Forward →",1),("Backward ←",-1)})
                {
                    int direction=option.Item2;
                    var item=new ToolStripMenuItem(option.Item1){Checked=spec.Direction==direction};
                    item.Click+=(sender,args)=>Change(parameter,s=>s.Direction=direction);
                    directions.DropDownItems.Add(item);
                }
                group.DropDownItems.Add(directions);
                var required=new ToolStripMenuItem("Required"){Checked=spec.Required};
                required.Click+=(sender,args)=>Change(parameter,s=>s.Required=!s.Required);
                group.DropDownItems.Add(required);
                var left=new ToolStripMenuItem("Move left"){Enabled=slot>0};
                var right=new ToolStripMenuItem("Move right"){Enabled=slot<Params.Input.Count-3};
                left.Click+=(sender,args)=>Move(index,-1);right.Click+=(sender,args)=>Move(index,1);
                group.DropDownItems.Add(left);group.DropDownItems.Add(right);
                menu.Items.Add(group);
            }
            void AddPreset(string title,(string Kind,int Direction)[] sequence)
            {var item=new ToolStripMenuItem(title);item.Click+=(sender,args)=>ApplyPreset(title,sequence);presets.DropDownItems.Add(item);}
        }

        private void Change(IGH_Param param,Action<Spec> change)
        {
            if(!Params.Input.Contains(param))return;
            RecordUndoEvent("Edit profile element");
            var spec=ReadSpec(param);change(spec);WriteSpec(param,spec);
            ExpireSolution(true);
        }
        private void ApplyPreset(string title,(string Kind,int Direction)[] sequence)
        {
            if(Params.Input.Count!=3||Params.Input[2].SourceCount!=0)return;
            RecordUndoEvent("Create "+title+" profile preset");
            Params.UnregisterInputParameter(Params.Input[2]);
            foreach(var item in sequence)
            {Params.RegisterInputParam(MakeParam(new Spec{Kind=item.Kind,Direction=item.Direction,Required=item.Kind!="Median"}));}
            Params.OnParametersChanged();VariableParameterMaintenance();ExpireSolution(true);
        }
        private void Move(int index,int delta)
        {
            int next=index+delta;
            if(index<2||next<2||next>=Params.Input.Count)return;
            RecordUndoEvent("Reorder profile element");
            var keys=Enumerable.Range(0,Params.Input.Count).ToArray();keys[index]=next;keys[next]=index;
            Params.SortInput(keys);
            Params.OnParametersChanged();VariableParameterMaintenance();ExpireSolution(true);
        }

        protected override void SolveInstance(IGH_DataAccess da)
        {
            string street="Rua",type="Custom";da.GetData(0,ref street);da.GetData(1,ref type);
            var profile=new StreetProfile{Street=string.IsNullOrWhiteSpace(street)?"Rua":street.Trim(),
                Type=string.IsNullOrWhiteSpace(type)?"Custom":type.Trim()};
            for(int i=2;i<Params.Input.Count;i++)
            {
                var spec=ReadSpec(Params.Input[i]);double minimum=0,maximum=0;
                var data=Params.Input[i].VolatileData.AllData(true).FirstOrDefault();
                if(data!=null&&!ParseWidthDomain(data,out minimum,out maximum))
                {AddRuntimeMessage(GH_RuntimeMessageLevel.Error,"Width Domain inválido em "+Params.Input[i].NickName+". Use mínimo|máximo, intervalo GH ou número fixo.");return;}
                profile.Elements.Add(new StreetProfileElement{Id=Params.Input[i].InstanceGuid,Type=spec.Kind,
                    Direction=spec.Direction,Required=spec.Required,PreferredWidth=maximum,
                    WidthDomain=new StreetWidthDomain(minimum,maximum),Status="NOMINAL"});
            }
            var warnings=StreetProfileTypeValidator.Validate(profile).ToList();
            foreach(var warning in warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,warning);
            da.SetData(0,new GH_ObjectWrapper(profile));
            da.SetDataList(1,profile.Elements.Select(x=>x.ToString()));
            da.SetData(2,profile.ToString()+(warnings.Count>0?" | "+string.Join("; ",warnings):""));
            Message=warnings.Count>0?"PROFILE WARNING":profile.Elements.Count+" faixas";
        }
        private static bool ParseWidthDomain(IGH_Goo goo,out double minimum,out double maximum)
        {
            minimum=0;maximum=0;
            object value=goo is GH_ObjectWrapper wrapped?wrapped.Value:
                goo is GH_Interval intervalGoo?(object)intervalGoo.Value:
                goo is GH_Number number?(object)number.Value:
                goo is GH_Integer integer?integer.Value:
                goo is GH_String text?text.Value:(object)goo.ToString();
            if(value is StreetProfileElement e)
            {minimum=e.MinimumWidth;maximum=e.MaximumWidth;return ValidDomain(minimum,maximum);}
            if(value is Interval interval)
            {minimum=interval.T0;maximum=interval.T1;return ValidDomain(minimum,maximum);}
            string[] fields=Convert.ToString(value,CultureInfo.InvariantCulture).Split('|');
            if(fields.Length<1||fields.Length>2||!double.TryParse(fields[0].Trim(),NumberStyles.Float,
                CultureInfo.InvariantCulture,out minimum))return false;
            maximum=minimum;
            if(fields.Length==2&&!double.TryParse(fields[1].Trim(),NumberStyles.Float,
                CultureInfo.InvariantCulture,out maximum))return false;
            return ValidDomain(minimum,maximum);
        }
        private static bool ValidDomain(double minimum,double maximum) => minimum>=0 && maximum>=minimum &&
            !double.IsNaN(minimum) && !double.IsInfinity(minimum) &&
            !double.IsNaN(maximum) && !double.IsInfinity(maximum);
    }
}
