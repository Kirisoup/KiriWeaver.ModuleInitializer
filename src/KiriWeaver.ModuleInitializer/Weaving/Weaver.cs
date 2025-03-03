using System.Reflection;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace KiriWeaver.ModuleInitializer.Weaving;

public class Weaver : Microsoft.Build.Utilities.Task
{
	[Required]
	public required string InputAssembly { get; set; }

	[Required]
	public required string IntermediateAssembly { get; set; }

	[Required]
	public required string OutputAssembly { get; set; }

	public override bool Execute()
	{
		try {
			using (var output = Weave(File.Exists(IntermediateAssembly)
				? IntermediateAssembly 
				: InputAssembly,
				out var succeed)) 
			{
				if (output is null) return succeed;
				output.Write(OutputAssembly);
			}

			var dir = Path.GetDirectoryName(IntermediateAssembly);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

			File.Copy(OutputAssembly, IntermediateAssembly, overwrite: true);

			return true;
		}
		catch (Exception ex) {
			Log.LogError($"""
				{nameof(ModuleInitializer)} weaving failed because {ex.Message}
				StackTrace: 
				{ex.StackTrace}
				""");
			return false;
		}
	}

	private AssemblyDefinition? Weave(string inputPath, out bool succeed) {
		succeed = true;
		try {
			var assembly = AssemblyDefinition.ReadAssembly(inputPath);
			var attrType = typeof(ModuleInitializerAttribute).FullName;

			(var init, var attr) = assembly.MainModule.GetTypes()
				.Where(t => !t.IsAbstract || (t.IsAbstract && t.IsSealed))
				.SelectMany(t => t.Methods)
				.Where(m => m.IsStatic && m.Parameters.Count == 0)
				.Select(method => (method, attr: method.CustomAttributes
					.FirstOrDefault(attr => attr.AttributeType.FullName == attrType)))
				.FirstOrDefault(method => method.attr is not null);

			if (init is null || attr is null) return null;

			init.CustomAttributes.Remove(attr);
			var reference = assembly.MainModule.AssemblyReferences
				.FirstOrDefault(r => r.Name == Assembly.GetExecutingAssembly().GetName().Name);
			if (reference is not null) 
				assembly.MainModule.AssemblyReferences.Remove(reference);

			var cctor = new MethodDefinition(
				name: ".cctor", 
				attributes: 
					MethodAttributes.Static | 
					MethodAttributes.SpecialName | 
					MethodAttributes.RTSpecialName,
				returnType: assembly.MainModule.ImportReference(init.ReturnType));

			var il = cctor.Body.GetILProcessor();
			il.Append(il.Create(OpCodes.Call, init));
			il.Append(il.Create(OpCodes.Ret));

			assembly.MainModule.GetType("<Module>").Methods.Add(cctor);
			return assembly;
		} catch (Exception ex) {
			Log.LogError($"""
				{nameof(ModuleInitializer)} weaving failed because {ex.Message}
				StackTrace: 
				{ex.StackTrace}
				""");
			succeed = false;
			return null;
		}
	}
}