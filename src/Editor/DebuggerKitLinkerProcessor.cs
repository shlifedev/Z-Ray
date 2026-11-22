using System.IO;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.UnityLinker;

namespace Lumera.DebuggerKit.Editor
{
    // 치트는 리플렉션으로만 불리므로, 코드 스트리핑(IL2CPP 빌드)이 쓰이지 않는 코드로 보고 지울 수 있다.
    // 빌드할 때 디버거 킷을 참조하는 어셈블리를 통째로 보존하는 link.xml을 만들어 링커에 넘긴다.
    // 디버거 킷은 Development Build에서만 컴파일되므로, 릴리스 빌드에서는 보존할 어셈블리가 없어 아무것도 하지 않는다.
    internal sealed class DebuggerKitLinkerProcessor : IUnityLinkerProcessor
    {
        private const string CORE_ASSEMBLY = "Lumera.DebuggerKit";
        private const string OUTPUT_PATH = "Library/Lumera.DebuggerKit/link.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            StringBuilder xml = new StringBuilder("<linker>\n");
            int count = 0;
            foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies))
            {
                if (assembly.name != CORE_ASSEMBLY && !References(assembly))
                {
                    continue;
                }

                xml.Append("  <assembly fullname=\"").Append(assembly.name).Append("\" preserve=\"all\" />\n");
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            xml.Append("</linker>\n");
            string path = Path.GetFullPath(OUTPUT_PATH);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, xml.ToString());
            return path;
        }

        private static bool References(Assembly assembly)
        {
            foreach (Assembly reference in assembly.assemblyReferences)
            {
                if (reference.name == CORE_ASSEMBLY)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
