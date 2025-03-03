using KiriWeaver.Task;
using KiriLib.ErrorHandling;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace KiriWeaver.ModuleInitializer.Weaving;

public sealed class Task() : WeaverTask(nameof(ModuleInitializer))
{
	public override Result<AssemblyDefinition?, Exception> Weave(string inputAssembly) {
		try {
			var assembly = AssemblyDefinition.ReadAssembly(inputAssembly);
			var attrType = typeof(ModuleInitializerAttribute).FullName;

			(var init, var attr) = assembly.MainModule.GetTypes()
				.Where(t =>  t.IsSealed || !t.IsAbstract)
				.SelectMany(t => t.Methods
					.Where(m => m.IsStatic && m.Parameters.Count == 0))
				.Select(method => (method, attr: method.CustomAttributes
					.FirstOrDefault(attr => attr.AttributeType.FullName == 
						typeof(ModuleInitializerAttribute).FullName)))
				.FirstOrDefault(method => method.attr is not null);

			if (init is null) return Result.Ok<AssemblyDefinition?>(null);

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

			return Result.Ok<AssemblyDefinition?>(assembly);
		} catch (Exception ex) {
			return Result.Ex(ex);
		}
	}

}