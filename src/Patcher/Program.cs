using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ForgeTaxPatcher;

/// <summary>
/// Injects the ForgeCheat mod into ForgeTax's Assembly-CSharp.dll.
///
///  1. ForgeFramePacing.Boot  -> prepend a call to ForgeCheat.Bootstrap.Boot()
///  2. ForgeChances.Roll      -> delegate to ForgeCheat.Hooks.Roll(ref ForgeChances, int)
///  3. ForgeReworkRules.Speed -> delegate to ForgeCheat.Hooks.Speed(ForgeCampaign, bool)
///
/// The unpatched assembly is kept as Assembly-CSharp.dll.orig and restored on every run,
/// so this tool is idempotent and can be re-run after a game update.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string gameDir = args.Length > 0 ? args[0] : @"E:\ForgeTax v26.10.12";
        string modSource = args.Length > 1
            ? args[1]
            : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ForgeCheat", "bin", "Release", "ForgeCheat.dll");

        string managedDir = Path.Combine(gameDir, "ForgeTax_Data", "Managed");
        string targetPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
        string backupPath = targetPath + ".orig";
        string modTargetPath = Path.Combine(managedDir, "ForgeCheat.dll");

        Console.WriteLine("Game dir   : " + gameDir);

        if (!File.Exists(targetPath))
        {
            Console.Error.WriteLine("ERROR: not found: " + targetPath);
            return 1;
        }

        if (!File.Exists(modSource))
        {
            Console.Error.WriteLine("ERROR: ForgeCheat.dll not found: " + modSource);
            return 1;
        }

        if (!File.Exists(backupPath))
        {
            File.Copy(targetPath, backupPath);
            Console.WriteLine("Backup     : " + backupPath);
        }
        else
        {
            File.Copy(backupPath, targetPath, overwrite: true);
            Console.WriteLine("Restored   : Assembly-CSharp.dll from .orig");
        }

        File.Copy(modSource, modTargetPath, overwrite: true);
        Console.WriteLine("Mod        : " + modTargetPath);

        using ModuleDefinition mod = ModuleDefinition.ReadModule(modTargetPath);

        MethodDefinition bootHook = Require(mod, "ForgeCheat.Bootstrap", "Boot");
        MethodDefinition rollHook = Require(mod, "ForgeCheat.Hooks", "Roll");
        MethodDefinition speedHook = Require(mod, "ForgeCheat.Hooks", "Speed");

        using ModuleDefinition game = ModuleDefinition.ReadModule(targetPath, new ReaderParameters { ReadWrite = true });

        HookBoot(game, bootHook);
        RewriteRoll(game, rollHook);
        RewriteSpeed(game, speedHook);

        game.Write();
        Console.WriteLine("Patched    : " + targetPath);
        Console.WriteLine("Done.");
        return 0;
    }

    private static MethodDefinition Require(ModuleDefinition module, string typeName, string methodName)
    {
        TypeDefinition type = module.GetType(typeName);
        if (type == null)
        {
            throw new InvalidOperationException("Missing type in mod: " + typeName);
        }

        foreach (MethodDefinition method in type.Methods)
        {
            if (method.Name == methodName)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Missing method in mod: " + typeName + "." + methodName);
    }

    private static TypeDefinition RequireType(ModuleDefinition module, string name)
    {
        TypeDefinition type = module.GetType(name);
        if (type == null)
        {
            throw new InvalidOperationException("Game type not found: " + name + " (game build changed?)");
        }

        return type;
    }

    private static MethodDefinition RequireMethod(TypeDefinition type, string name, int parameterCount)
    {
        foreach (MethodDefinition method in type.Methods)
        {
            if (method.Name == name && method.Parameters.Count == parameterCount)
            {
                return method;
            }
        }

        throw new InvalidOperationException("Game method not found: " + type.FullName + "." + name + "/" + parameterCount);
    }

    private static void HookBoot(ModuleDefinition game, MethodDefinition hook)
    {
        TypeDefinition pacing = RequireType(game, "ForgeFramePacing");
        MethodDefinition boot = RequireMethod(pacing, "Boot", 0);

        if (!boot.HasBody)
        {
            throw new InvalidOperationException("ForgeFramePacing.Boot has no body.");
        }

        MethodReference call = game.ImportReference(hook);
        ILProcessor il = boot.Body.GetILProcessor();
        il.InsertBefore(boot.Body.Instructions[0], il.Create(OpCodes.Call, call));
        boot.Body.MaxStackSize = Math.Max(boot.Body.MaxStackSize, 8);

        Console.WriteLine("Hook       : ForgeFramePacing.Boot -> " + call.FullName);
    }

    private static void RewriteRoll(ModuleDefinition game, MethodDefinition hook)
    {
        TypeDefinition chances = RequireType(game, "ForgeChances");
        MethodDefinition roll = RequireMethod(chances, "Roll", 1);

        MethodReference call = game.ImportReference(hook);
        ClearBody(roll);
        ILProcessor il = roll.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0)); // ForgeChances& this
        il.Append(il.Create(OpCodes.Ldarg_1)); // int ticket
        il.Append(il.Create(OpCodes.Call, call));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("Rewrite    : ForgeChances.Roll -> " + call.FullName);
    }

    private static void RewriteSpeed(ModuleDefinition game, MethodDefinition hook)
    {
        TypeDefinition rules = RequireType(game, "ForgeReworkRules");
        MethodDefinition speed = RequireMethod(rules, "Speed", 2);

        MethodReference call = game.ImportReference(hook);
        ClearBody(speed);
        ILProcessor il = speed.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ldarg_0)); // ForgeCampaign
        il.Append(il.Create(OpCodes.Ldarg_1)); // bool fever
        il.Append(il.Create(OpCodes.Call, call));
        il.Append(il.Create(OpCodes.Ret));

        Console.WriteLine("Rewrite    : ForgeReworkRules.Speed -> " + call.FullName);
    }

    private static void ClearBody(MethodDefinition method)
    {
        if (!method.HasBody)
        {
            throw new InvalidOperationException(method.FullName + " has no body to rewrite.");
        }

        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();
        method.Body.ExceptionHandlers.Clear();
        method.Body.MaxStackSize = 8;
    }
}
