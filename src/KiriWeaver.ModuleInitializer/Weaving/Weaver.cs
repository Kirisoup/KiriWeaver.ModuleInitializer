using Microsoft.Build.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace KiriWeaver.ModuleInitializer.Weaving;

public class Weaver : Microsoft.Build.Utilities.Task
{
	[Required]
	public required string InputAssembly { get; set; }

	[Required]
	public required string OutputAssembly { get; set; }

	public override bool Execute()
	{
		try {
            using var assembly = AssemblyDefinition.ReadAssembly(InputAssembly);
			var attrType = typeof(ModuleInitializerAttribute).FullName;

			var init = assembly.MainModule.GetTypes()
				.Where(t => t.IsAbstract && t.IsSealed)
				.SelectMany(t => t.Methods)
				.Where(m => m.IsStatic && m.Parameters.Count == 0 && m.CustomAttributes
					.Any(attr => attr.AttributeType.FullName == attrType))
				.FirstOrDefault();

			if (init is null) return true;

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
			assembly.Write(OutputAssembly);

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
}