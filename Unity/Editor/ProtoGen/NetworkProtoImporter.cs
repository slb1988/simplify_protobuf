using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mogo;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using Asset = UnityEditor.VersionControl.Asset;

/// <summary>
/// 网络协议导入器
/// </summary>
public class NetworkProtoImporter
{
    // [MenuItem("Tools/网络/Test")]
    // static void Test()
    // {
    //     string str = "asdffads[MapContent]asdfasdf";
    //     str = File.ReadAllText("Assets/Editor/ProtoGen/CommandMap.template");
    //
    //     str = str.Replace("[MapContent]", "");
    //
    //     str = "";
    // }
    [MenuItem("Tools/网络Proto生成")]
    static void Import()
    {
        List<string> outputList = new List<string>();
        var bashPath = Path.GetFullPath(Application.dataPath + "/../../Proto");
        var protoDirPath = "Assets/Scripts/ProtoGen/";
        var originFiles = FileUtils.GetFiles(protoDirPath, ".cs", SearchOption.TopDirectoryOnly);
        var assetList = new AssetList();
        foreach (var file in originFiles)
        {
            assetList.Add(new UnityEditor.VersionControl.Asset(file));
        }
        Provider.Checkout(assetList, CheckoutMode.Asset).Wait();
        Debug.Log(bashPath);

        string genSrcPath = $"{bashPath}/src";
        if (Directory.Exists(genSrcPath))
        {
            FileUtils.DeleteFiles(genSrcPath);
        }
        else
        {
            Directory.CreateDirectory(genSrcPath);
        }
        
        CommandLineHelper.Run("cmd.exe", "/k export.bat", outputList, bashPath);
        foreach (var info in outputList)
        {
            if (info.Contains("error"))
                Log.E(info);
        }
        
        var files = FileUtils.GetFiles(genSrcPath);
        foreach (var file in files)
        {
            var tarPath = protoDirPath + Path.GetFileName(file);
            File.Copy(file, tarPath, true);
        }

        // 就的文件名，后面不存在，说明需要删除
        foreach (var file in originFiles)
        {
            var fileName = Path.GetFileName(file);
            if (fileName == "CommandMap.cs")
                continue;
            if (fileName == "NetPacket.Gen.cs")
                continue;
            
            bool find = false;
            for (int i = 0; i < files.Length; i++)
            {
                if (Path.GetFileName(files[i]) == fileName)
                {
                    find = true;
                    break;
                }
            }

            if (!find)
            {
                Provider.Delete(new UnityEditor.VersionControl.Asset(file)).Wait();
            }
        }

        #region 导出协议CommandMap id->type

        StringBuilder msgTypeStrb = new StringBuilder();
        StringBuilder sb = new StringBuilder();
        StringBuilder getDataStrb = new StringBuilder();
        foreach (var file in Directory.GetFiles(bashPath))
        {
            if (file.EndsWith(".proto") == false)
                continue;

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.StartsWith("message "))
                {
                    var messageName = line.Substring(8, line.LastIndexOf('{') - 8);
                    messageName = messageName.Trim();
                    if (lines[i+1].Contains("option (msg_id)") == false)
                        continue;

                    var startIdx2 = "  option (msg_id) =".Length;
                    var idStr = lines[i + 1].Substring(startIdx2, lines[i+1].LastIndexOf(';')-startIdx2);
                    idStr = idStr.Trim();

                    sb.Append($"			cmdMap.Add((int)Pb.MessageType.{messageName}, typeof({messageName}));\r\n");
                    msgTypeStrb.Append($"\t\t{messageName} = {idStr},\r\n");

                    getDataStrb.Append(
                        $"\t\t\tif (type == typeof(Pb.{messageName})) \n\t\t\t\treturn usePool ? Mogo.ObjectPoolThreadSafe<Pb.{messageName}>.Allocate() : new Pb.{messageName}();\n");
                    
                    i++;
                }
            }
        }

        var additionRepeatFieldTypes = new string[]
        {
            "ClientComponent",
            "PlayerEvent",
            "WorldEvent",
            "PlayerEventInfo",
            "RewardData",
            "ChatMsg",
            "PlayerSnsInfo",
            "TaskConditionData",
        };
        foreach (var messageName in additionRepeatFieldTypes)
        {
            
            getDataStrb.Append(
                $"\t\t\tif (type == typeof(Pb.{messageName})) \n\t\t\t\treturn usePool ? Mogo.ObjectPoolThreadSafe<Pb.{messageName}>.Allocate() : new Pb.{messageName}();\n");

        }

        var content = File.ReadAllText("Assets/Editor/ProtoGen/CommandMap.template");
        content = content.Replace("[[MapContent]]", sb.ToString());
        content = content.Replace("[[MessageType]]", msgTypeStrb.ToString());
        content = content.Replace("[[GetDataContent]]", getDataStrb.ToString());
        var commandMapPath = "Assets/Scripts/ProtoGen/CommandMap.cs";
        var commandMapAsset = new UnityEditor.VersionControl.Asset(commandMapPath);
        assetList.Add(commandMapAsset);
        Provider.Checkout(commandMapAsset, CheckoutMode.Asset).Wait();
        File.WriteAllText(commandMapPath, content);

        #endregion

        //== 可能是之前的Asset状态发生了变化，不能直接拿来用
        // 重新克隆一份用作revert吧
        var newAssetList = new AssetList();
        foreach (var ass in assetList)
        {
            var newAsset = Provider.GetAssetByPath(ass.assetPath);
            newAssetList.Add(newAsset);
        }
        
        Provider.Revert(newAssetList, RevertMode.Unchanged).Wait();
        
        AssetDatabase.Refresh();
    }
}
