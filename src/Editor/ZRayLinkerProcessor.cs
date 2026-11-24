using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace Lumera.ZRay.Editor
{
    // 리플렉션으로 호출할 치트를 스트리핑에서 보존한다. 개별 빌드의 추가 심볼까지 적용된 플레이어 DLL을 읽는다.
    internal sealed class ZRayLinkerProcessor : IUnityLinkerProcessor
    {
        private const string CORE_ASSEMBLY = "Lumera.ZRay";
        private const string OUTPUT_PATH = "Library/Lumera.ZRay/link.xml";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            BuildFile[] files = report.GetFiles();
            if (!Array.Exists(files, static file => file.role == CommonRoles.managedLibrary && Path.GetFileName(file.path) == CORE_ASSEMBLY + ".dll"))
            {
                return null;
            }

            XElement linker = new XElement("linker");
            foreach (BuildFile file in files)
            {
                if (file.role != CommonRoles.managedLibrary && file.role != CommonRoles.dependentManagedLibrary)
                {
                    continue;
                }

                string name = GetCheatAssemblyNameOrNull(file.path);
                if (name != null)
                {
                    linker.Add(new XElement("assembly", new XAttribute("fullname", name), new XAttribute("preserve", "all")));
                }
            }

            string path = Path.GetFullPath(OUTPUT_PATH);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            linker.Save(path);
            return path;
        }

        private static string GetCheatAssemblyNameOrNull(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using PEReader reader = new PEReader(stream);
            MetadataReader metadata = reader.GetMetadataReader();
            string name = metadata.GetString(metadata.GetAssemblyDefinition().Name);
            if (name == CORE_ASSEMBLY)
            {
                return name;
            }

            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                AssemblyReference reference = metadata.GetAssemblyReference(handle);
                if (metadata.StringComparer.Equals(reference.Name, CORE_ASSEMBLY))
                {
                    return name;
                }
            }

            return null;
        }
    }
}
