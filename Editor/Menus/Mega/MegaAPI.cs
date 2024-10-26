using CG.Web.MegaApiClient;
using HarmonyLib;
using log4net.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using static VRC.Core.ApiFile;

namespace SDKTools
{
    public class MegaAPI
    {
        public static MegaApiClient client;

        public static List<INode> data;

        /*
        public static void Login(string email, string password)
        {
            client = new MegaApiClient();
            client.Login(email, password);

            IEnumerable<INode> nodes = client.GetNodes();

            INode parent = nodes.Single(n => n.Type == NodeType.Root);

            DisplayNodesRecursive(nodes, parent);

            client.Logout();
        }
        */

        public static bool DownloadFolder(string link)
        {
            MegaApiClient client = new MegaApiClient();
            client.LoginAnonymous();

            Uri folderLink = new Uri(link);
            IEnumerable<INode> nodes = client.GetNodesFromLink(folderLink);
            foreach (INode node in nodes.Where(x => x.Type == NodeType.File))
            {
                string parents = GetParents(node, nodes);
                Directory.CreateDirectory(parents);
                Console.WriteLine($"Downloading {parents}\\{node.Name}");
                client.DownloadFile(node, Path.Combine(parents, node.Name));
            }

            client.Logout();

            return true;
        }

        public static bool DownloadFile(string link)
        {
            MegaApiClient client = new MegaApiClient();
            client.LoginAnonymous();

            Uri fileLink = new Uri(link);
            INode node = client.GetNodeFromLink(fileLink);

            Console.WriteLine($"Downloading {node.Name}");
            client.DownloadFile(fileLink, node.Name);

            client.Logout();

            return true;
        }

        public static bool DownloadFile(string[] info)
        {
            MegaApiClient client = new MegaApiClient();
            client.LoginAnonymous();

            Uri fileLink = new Uri(info[1]);
            INode node = client.GetNodeFromLink(fileLink);

            Console.WriteLine($"Downloading {node.Name}");

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string destinationPath = Path.Combine(appDataPath, "SDKTools/");

            client.DownloadFile(fileLink, Path.Combine(destinationPath, "Downloads/" +node.Name));
            
            ImportPanel.fileCategories[info[2]].Add($"{info[0]}|{info[1]}|{info[2]}|{"Downloads/" + node.Name}");

            client.Logout();

            return true;
        }

        static string GetParents(INode node, IEnumerable<INode> nodes)
        {
            List<string> parents = new List<string>();
            while (node.ParentId != null)
            {
                INode parentNode = nodes.Single(x => x.Id == node.ParentId);
                parents.Insert(0, parentNode.Name);
                node = parentNode;
            }

            return string.Join("\\", parents);
        }
    }
}
