using System.Collections.Generic;

namespace NuvioPlayer.Interfaces;

public interface ICommandLineService
{
    List<string> ParseMediaPaths(IEnumerable<string> args);
}
