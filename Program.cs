using System;
using System.IO;
using System.Text.Json;
using System.Xml.Linq;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Net.WebRequestMethods;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Diagnostics;
using System.Security.AccessControl;
using System.IO.Pipes;
using System.Security.Principal;

class Program
{
    private static string current = @System.IO.Directory.GetCurrentDirectory();
    private static string LogFolder = Path.Combine(current, "Log");
    private static string AvailableFiles = Path.Combine(LogFolder, "Available.json");
    private static string LogFile = Path.Combine(LogFolder, "Logs.txt");
    private static string OddPartFiles = Path.Combine(current, "Odds");
    private static string PairPartFiles = Path.Combine(current, "Pairs");

    class AvailableObject
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public long size { get; set; }
        public string[] PartsPaths { get; set; }
    }
    class RootObject
    {
        public List<AvailableObject> availableObjects { get; set; } = new List<AvailableObject>();
    }

    static void Main(string[] args)
    {
       

        Checkup();
        string? filePath = null;
        string? fileNum = null;
        int choice, numchoice;
        RootObject rootObject = new RootObject();
        
        while(true)
        {
            Console.WriteLine("Select operation by number:\n'1-Upload'\n'2-Download'\n'3-exit' to quit application");
            string option  = Console.ReadLine();
            int.TryParse(option, out choice);
            //if (!int.TryParse(option, out choice))
             //   Console.WriteLine("Invalid command");
            switch (choice)
            {
                case 1:
                    Console.WriteLine("Please specify file path: ");
                    filePath = Console.ReadLine();
                    SplitFile(filePath, rootObject);
                    break;
                case 2:
                    Console.WriteLine("Available Files for download:");
                    int num = ReadJson();
                    Console.WriteLine("Please choose the number of the file to download");
                    fileNum = Console.ReadLine();
                    if(int.TryParse(fileNum, out numchoice))
                    {
                        if(numchoice <= num)
                        {
                            JoinFiles(num);
                        }
                        else
                        {
                            Console.WriteLine("Invalid number!");
                        }
                    }
                    break;
                case 3:
                    Console.WriteLine("Exit application");
                    System.Environment.Exit(1);
                    break;
                default:
                    Console.WriteLine("Invalid command");
                    break;
            }
        }
        
    }

    static void Checkup()
    {
        //current = System.IO.Directory.GetCurrentDirectory();
        //LogFolder = Path.Combine(current, "Log");
        //AvailableFiles = Path.Combine(LogFolder, "Available.json");
        //LogFile = Path.Combine(LogFolder, "Logs.txt");
        //OddPartFiles = Path.Combine(current, "Odds");
        //PairPartFiles = Path.Combine(current, "Pairs");

        if (!Directory.Exists(LogFolder))
            Directory.CreateDirectory(LogFolder);
        if(!System.IO.File.Exists(AvailableFiles))
            System.IO.File.Create(AvailableFiles);
        if (!System.IO.File.Exists(LogFile))
            System.IO.File.Create(LogFile);
        if (!Directory.Exists(OddPartFiles))
            Directory.CreateDirectory(OddPartFiles);
        if (!Directory.Exists(PairPartFiles))
            Directory.CreateDirectory(PairPartFiles);
    }

    //static void WriteJson( JObject obj)
    static void WriteJson(AvailableObject obj , RootObject root)
    {
        if (root.Equals(null))
            return;
        try
        {
            FileInfo fileInfo = new FileInfo(AvailableFiles);
            if (fileInfo.Length != 0)
            {
                var existingData = System.IO.File.ReadAllText(AvailableFiles);
                //var existingData = System.IO.File.ReadAllLines (AvailableFiles);
                //Console.WriteLine($"reading data from file:{existingData}");

                root.availableObjects = JsonConvert.DeserializeObject<List<AvailableObject>>(existingData);

                root.availableObjects.Add(new AvailableObject { Id = root.availableObjects.Count+1, Name = obj.Name, size = obj.size, PartsPaths = obj.PartsPaths });
            }
            else
            {
                //Write if no record available.
                root.availableObjects[0].Id = 1;
                //root.availableObjects.Add(obj);
            }
            string updatedJsonData = JsonConvert.SerializeObject(root.availableObjects, Formatting.Indented);
            //Console.WriteLine(updatedJsonData);
            System.IO.File.WriteAllText(AvailableFiles, updatedJsonData);
        }
        catch(Newtonsoft.Json.JsonException j)
        {
            Console.WriteLine(j.ToString());
        }
    }

    static int ReadJson()
    {
        int num =0 ;      
        try
        {
            string fileContent = System.IO.File.ReadAllText(AvailableFiles);
            // parse the JSON string into a JObject
            JArray jsonArray = JArray.Parse(fileContent);
            //Filter to get all the values from the porperty "Name"
            var propertyValues = jsonArray
                                .OfType<JObject>()
                                .Select(obj => obj.Property("Name"))
                                .Where(prop => prop != null && prop.Value.Type == JTokenType.String)
                                .Select(prop => (string)prop.Value)
                                .ToList();

            foreach (var (value, index) in propertyValues.Select((value, i) => (value, i)))
            {
                Console.WriteLine($"{index+1}-{value}");
            }
            num = propertyValues.Count();
        }
        catch (Newtonsoft.Json.JsonException j)
        {
            Console.WriteLine(j.ToString());
        }
        return num;

    }

    static void SplitFile(string filePath, RootObject root)
    {
        //Check if we are handling a real file
        if(filePath == null || Directory.Exists(filePath) || !System.IO.File.Exists(filePath))
        {
            Console.WriteLine("File path cannot be empty or a directory!");
            return;
        }
        Console.WriteLine("Begin split.......");
        AvailableObject AvailableObj = new AvailableObject();
        try
        {
            using (var inputFile = System.IO.File.OpenRead(filePath))
            {
                //write the name
                string FileName = filePath.Substring(filePath.LastIndexOf('\\') + 1);
                AvailableObj.Name = FileName;
                //Console.WriteLine("File name is:{0}", AvailableObj.Name);
                //write the size
                long size = inputFile.Length;
                //Console.WriteLine("File size:{0}",size);
                AvailableObj.size = size;
                int numParts = size < 250 * 1000000 ? 8 : 10;
                long chunkSize = size / numParts;
                //Console.WriteLine($"Chunk size: {chunkSize} - with {numParts} parts");

                byte[] buffer = new byte[chunkSize];
                string[] Parts = new string[numParts];

                for (int i = 0; i < numParts; i++)
                {
                    string partFilePath = i % 2 == 0 ? Path.Combine(PairPartFiles, $"{AvailableObj.Name}.Part-{i + 1}") :
                                                    Path.Combine(OddPartFiles, $"{AvailableObj.Name}.Part-{i + 1}");
                    Parts[i] = partFilePath;
                    using (var outputFile = System.IO.File.Create(partFilePath))
                    {
                        //Console.WriteLine($"Writing to file: {partFilePath}");

                        if (i == numParts - 1)
                        {
                            // last chunk may be larger than the others
                            long remainingBytes = size - (i * chunkSize);
                            buffer = new byte[remainingBytes];
                        }
                        inputFile.Read(buffer, 0, buffer.Length);
                        outputFile.Write(buffer, 0, buffer.Length);
                    }
                }
                AvailableObj.PartsPaths = Parts;
                Console.WriteLine("Split completed");
            }
            root.availableObjects.Add(AvailableObj);
            WriteJson(AvailableObj, root);
        }
        catch (IOException ex)
        {
            Console.WriteLine("Error creating file: " + ex.Message);
        }
    }

    static void JoinFiles(int num)
    {
        Console.WriteLine("Begin join");
        try
        {
            string property = "Id";
            string fileContent = System.IO.File.ReadAllText(AvailableFiles);
            //Parse the json file as array
            JArray jsonArray = JArray.Parse(fileContent);
            //Get the entry having the correct id num
            IEnumerable<JToken> targetObjects = jsonArray.Where(obj => obj[property].Value<int>() == num);
            // Extract the values of the parts property from the target objects
            IEnumerable<JToken> partsValues = targetObjects.SelectMany(o => o["PartsPaths"]);
            string[] partsArray = partsValues.Select(p => (string)p).ToArray();
            //Console.WriteLine("parts arrray: " + partsArray.Count());

            using (FileStream fileStream = new FileStream(@current, FileMode.Create,FileAccess.ReadWrite,FileShare.ReadWrite))
            {
                foreach (string partFilePath in partsArray)
                {
                    Console.WriteLine($"Reading from file: {partFilePath}");

                    using (var inputFile = System.IO.File.OpenRead(partFilePath))
                    {
                        fileStream.CopyTo(inputFile);
                        //inputFile.CopyTo(outputFile);
                    }

                    System.IO.File.Delete(partFilePath);
                }
                Console.WriteLine("Join completed");
                Process.Start(current);
            }
        }

        catch (Newtonsoft.Json.JsonException j)
        {
            Console.WriteLine(j.ToString());
        }
        
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Error creating file: " + ex.Message);
        }
        /*      using (var outputFile = System.IO.File.Create(@current))
            {
                foreach (string partFilePath in partsArray)
                {
                    Console.WriteLine($"Reading from file: {partFilePath}");

                    using (var inputFile = System.IO.File.OpenRead(partFilePath))
                    {
                        inputFile.CopyTo(outputFile);
                    }

                    System.IO.File.Delete(partFilePath);
                }
                Console.WriteLine("Join completed");
                Process.Start(current);

            }*/
    }
}
