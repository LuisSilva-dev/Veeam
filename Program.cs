using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
class Program
{
    static Dictionary<string, byte[]> UpdateFolderTree(string src)
    {
        Dictionary<string, byte[]> folderTree = new();
        string[] srcFiles = Directory.GetFileSystemEntries(src);
        for(int i = 0; i < srcFiles.Length; i++)
        {
            if(File.Exists(srcFiles[i]))
            {
                folderTree.Add(srcFiles[i], File.ReadAllBytes(srcFiles[i]));
            }
            else if(Directory.Exists(srcFiles[i]))
            {
                Dictionary<string, byte[]> newFolderTree = UpdateFolderTree(srcFiles[i]);
                foreach(var file in newFolderTree)
                {
                    folderTree.Add(file.Key, file.Value);
                }
            }
        }
        return folderTree;
    }

    static void StoreFolderTree(ref Dictionary<string, byte[]> folderTree, string saved)
    {
        string dataStructure = JsonSerializer.Serialize(folderTree);
        File.WriteAllText(saved, dataStructure);
    }

    static Dictionary<string, byte[]> NewFiles(string src, string log_path, string saved)
    {
        Dictionary<string, byte[]> newFiles = new();
        using FileStream streamJson = File.OpenRead(saved);
        byte[] buffer = new byte[streamJson.Length];
        streamJson.Read(buffer, 0, (int)streamJson.Length);
        streamJson.Close();
        Dictionary<string, byte[]>? dict = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(buffer);
        string[] srcFilesFolders = Directory.GetFileSystemEntries(src);
        MD5 md5 = MD5.Create();
        for(int i = 0; i < srcFilesFolders.Length; i++)
        {
            if(File.Exists(srcFilesFolders[i]))
            {
                if(dict.ContainsKey(srcFilesFolders[i]))
                {
                    bool dif = false;
                    byte[] treeFile = dict[srcFilesFolders[i]];
                    byte[] srcContent  = File.ReadAllBytes(srcFilesFolders[i]);
                    byte[] treeFileHash = md5.ComputeHash(treeFile);
                    byte[] srcContentHash = md5.ComputeHash(srcContent);
                    for(int j = 0; j < treeFileHash.Length; j++)
                    {
                        if(treeFileHash[i]!=srcContentHash[i])
                        {                            
                            dif = true;
                        }
                    }
                    if(dif)
                    {
                        File.AppendAllText(log_path, "The file stored in " + srcFilesFolders[i] + " has been modified.\n");
                    }
                    newFiles.Add(srcFilesFolders[i], srcContent);
                }
                else
                {
                    byte[] srcContent  = File.ReadAllBytes(srcFilesFolders[i]);
                    newFiles.Add(srcFilesFolders[i], srcContent);
                    File.AppendAllText(log_path, "The file stored in " + srcFilesFolders[i] + " has been created.\n");
                }
            }
            else if(Directory.Exists(srcFilesFolders[i]))
            {
                Dictionary<string, byte[]> newFolderFiles = NewFiles(srcFilesFolders[i], log_path, saved);
                foreach(var file in newFolderFiles)
                {
                    newFiles.Add(file.Key, file.Value);
                }
            }
        }
        return newFiles;
    }

    static List<string> RemoveFiles(string log_path, string saved, ref Dictionary<string, byte[]> newFiles)
    {
        List<string> removeFiles = new();
        using FileStream streamJson = File.OpenRead(saved);
        byte[] buffer = new byte[streamJson.Length];
        streamJson.Read(buffer, 0, (int)streamJson.Length);
        streamJson.Close();
        Dictionary<string, byte[]>? dict = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(buffer);
        MD5 md5 = MD5.Create();
        foreach(var file in dict)
        {
            if(!newFiles.ContainsKey(file.Key))
            {
                removeFiles.Add(file.Key);
                File.AppendAllText(log_path, "The file stored in " + file.Key + " has been removed.\n");
            }
            else
            {
                newFiles.Remove(file.Key);
            }
        }
        return removeFiles;
    }

    static void UpdateReplica(string src, string rep, Dictionary<string, byte[]> newFiles, List<string> removeFiles)
    {
        foreach(var file in newFiles)
        {
            string rep_path = rep + @"\" + file.Key.Remove(file.Key.IndexOf(src), src.Length);
            new FileInfo(rep_path).Directory?.Create();
            File.WriteAllBytes(rep_path, file.Value);
        }
        foreach(var file in removeFiles)
        {
            string rep_path = rep + @"\" + file.Remove(file.IndexOf(src), src.Length);
            File.Delete(rep_path);
        }
    }

    static void Main(string[] args)
    {
        Dictionary<string, byte[]> folderTree = new(); 
        if(args.Length < 4)
        {
            Console.WriteLine("Four arguments are needed to run this program: source_path, replica_path, synchronization_interval in seconds and log_file_path");
        }
        else
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            float interTime = float.Parse(args[2])*1000;
            string src = args[0];
            string rep = args[1];
            string log = args[3];
            if(!Directory.Exists(src))
            {
                Console.WriteLine("Source doesn't exist.");
                return;
            }
            if(!Directory.Exists(rep))
            {
                Directory.CreateDirectory(rep);
            }
            if(!Directory.Exists(log))
            {
                Directory.CreateDirectory(log);
            }
            if(interTime < 5000)
            {
                Console.WriteLine("The interval can't be lower than 5 seconds");
                return;
            }
            string saved_path = Directory.GetParent(src).ToString() + @"\Saved";
            if(!Directory.Exists(saved_path))
            {
                Directory.CreateDirectory(saved_path);
            }
            string saved_file_path = saved_path + @"\saved.txt";
            if(!File.Exists(saved_file_path))
            {
                File.Create(saved_file_path).Close();
                StoreFolderTree(ref folderTree, saved_file_path);
            }
            interTime = 0.0f;
            do {
                if(stopwatch.ElapsedMilliseconds >= interTime)
                {
                    Dictionary<string, byte[]> newFiles = NewFiles(src, log + @"\log.txt", saved_file_path);
                    List<string> removeFiles = RemoveFiles(log + @"\log.txt", saved_file_path, ref newFiles);
                    UpdateReplica(src, rep, newFiles, removeFiles);
                    folderTree = UpdateFolderTree(src);
                    StoreFolderTree(ref folderTree, saved_file_path);
                    stopwatch.Restart();
                    Console.WriteLine("Replica updated.");
                }
                interTime = float.Parse(args[2])*1000;
            }while(true);
        }
    }
}