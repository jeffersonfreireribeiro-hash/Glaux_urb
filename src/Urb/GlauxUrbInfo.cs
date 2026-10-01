using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Buraqueira_Urb
{
    /// <summary>
    /// Metadados oficiais do plugin Buraqueira Urb para o Grasshopper.
    /// </summary>
    public class BuraqueiraUrbAssemblyInfo : GH_AssemblyInfo
    {
        public override string Name => "Glaux Urb";
        public override string Description => "Suíte de Desenho Urbano, Infraestrutura Viária, Seções Transversais Paramétricas e Loteamento para o Grasshopper.";
        public override string AuthorName => "Buraqueira Team";
        public override string AuthorContact => "";
        public override string Version => typeof(BuraqueiraUrbAssemblyInfo).Assembly.GetName().Version.ToString(3);
        public override Bitmap Icon => GlauxUrbIcons.PluginTabIcon;
        public override Bitmap AssemblyIcon => GlauxUrbIcons.PluginTabIcon;
        public override Guid Id => new Guid("8e2b1c4a-6d3f-4e5a-9a7b-1c2d3e4f5a6b");
    }

    /// <summary>
    /// Registra o ícone oficial e a ordem na aba/categoria 'Buraqueira Urb' no topo da Ribbon do Grasshopper.
    /// </summary>
    public class BuraqueiraUrbPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            try
            {
                Grasshopper.Instances.ComponentServer.AddCategoryIcon("Glaux Urb", GlauxUrbIcons.PluginTabIcon);
                Grasshopper.Instances.ComponentServer.AddCategorySymbolName("Glaux Urb", 'G');
            }
            catch { }
            return GH_LoadingInstruction.Proceed;
        }
    }
}
