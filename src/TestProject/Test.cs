using System;
using KiriWeaver.ModuleInitializer;

namespace TestProject;

public static class VeryRandomClass
{
	[ModuleInitializer]
	public static void HelloWorld() {
		Console.WriteLine("Hello world");
	}

	public static void Main() {}
}

