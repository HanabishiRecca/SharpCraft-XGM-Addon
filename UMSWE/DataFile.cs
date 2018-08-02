using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class DataFile {

    Dictionary<string, List<Parameter>> Data = new Dictionary<string, List<Parameter>>();

    public DataFile(string filePath) {
        ApplyPatch(filePath, PatchMode.Addon);
    }

    public void ApplyPatch(string filePath, PatchMode mode) {
        var strs = File.ReadAllLines(filePath, Encoding.UTF8);

        string lastSection = null;
        for(int i = 0; i < strs.Length; i++) {
            var str = strs[i];
            if(str.Length < 1)
                continue;

            var comm = str.IndexOf("//");
            if(comm > -1) {
                str = str.Substring(0, comm);
                if(str.Length < 1)
                    continue;
            }

            var sub = str.TrimStart();
            if(sub.Length < 1)
                continue;

            if(sub[0] == '[') {
                var cbi = sub.IndexOf(']', 1);
                if(cbi > 0) {
                    var section = sub.Substring(0, cbi + 1);
                    lastSection = section;
                    if(!Data.ContainsKey(section))
                        Data[section] = new List<Parameter>();
                }
            } else if(lastSection != null) {
                var esi = str.IndexOf('=', 1);
                if(esi > 0) {
                    string name = str.Substring(0, esi), value = str.Substring(esi + 1);
                    Parameter param;
                    if((mode == PatchMode.Addon) || ((param = SeekParam(Data[lastSection], name)) == null))
                        Data[lastSection].Add(new Parameter { Name = name, Value = value });
                    else {
                        var strRep = value.IndexOf("%s%");
                        if(strRep > -1) {
                            var hasBraces = ((param.Value[0] == '"') && (param.Value[param.Value.Length - 1] == '"'));
                            if(hasBraces)
                                param.Value = param.Value.Substring(0, param.Value.Length - 1);
                            param.Value = $"{value.Substring(0, strRep)}{param.Value}{value.Substring(strRep + 3)}{(hasBraces ? "\"" : "")}";

                        } else
                            param.Value = value;
                    }
                }
            }
        }

    }

    static Parameter SeekParam(List<Parameter> section, string name) {
        for(int i = 0; i < section.Count; i++) {
            var param = section[i];
            if(param.Name == name)
                return param;
        }
        return null;
    }

    public void SaveAs(string filePath) {
        using(var sw = new StreamWriter(filePath, false, Encoding.UTF8)) {
            for(int i = 0; i < Data.Count; i++) {
                var section = Data.ElementAt(i);
                sw.WriteLine(section.Key);

                for(int j = 0; j < section.Value.Count; j++) {
                    var param = section.Value[j];
                    sw.WriteLine($"{param.Name}={param.Value}");
                }

                sw.WriteLine();
            }

            sw.Close();
        }
    }

    class Parameter {
        public string Name, Value;
    }

}

enum PatchMode {
    Addon,
    Patch,
}
