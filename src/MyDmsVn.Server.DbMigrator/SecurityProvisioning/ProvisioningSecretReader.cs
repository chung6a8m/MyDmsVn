using System;
using System.Collections.Generic;
using System.IO;

namespace MyDmsVn.Server.DbMigrator.SecurityProvisioning;

public interface IProvisioningSecretReader
{
    bool IsInteractive { get; }

    string ReadSecret(string prompt, TextWriter output);
}

public sealed class ConsoleProvisioningSecretReader : IProvisioningSecretReader
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public string ReadSecret(string prompt, TextWriter output)
    {
        if (!IsInteractive)
        {
            throw new InvalidOperationException("Interactive masked input is required for enrollment.");
        }

        output.Write(prompt);
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                output.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                {
                    characters.RemoveAt(characters.Count - 1);
                }

                continue;
            }

            if (key.Key == ConsoleKey.C &&
                (key.Modifiers & ConsoleModifiers.Control) == ConsoleModifiers.Control)
            {
                throw new OperationCanceledException("Secret entry was cancelled.");
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }
}
