﻿using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using KFreonLib.Debugging;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;

namespace ME3Explorer.AnimationExplorer
{
    public partial class AnimationExplorer : WinFormsBase
    {
        public List<AnimTree> AT;
        public List<AnimSet> AS;
        public List<string> filenames = new List<string>();

        public AnimationExplorer()
        {
            InitializeComponent();
        }

        private void openPccToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.pcc|*.pcc";
            if (d.ShowDialog() == DialogResult.OK)
                LoadPcc(d.FileName);
        }

        public void LoadPcc(string s)
        {
            try
            {
                LoadME3Package(s);
                reScan();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex.Message);
            }
        }

        private void reScan()
        {
            AT = new List<AnimTree>();
            AS = new List<AnimSet>();
            for (int i = 0; i < pcc.ExportCount; i++)
            {
                IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                switch (Exports[i].ClassName)
                {
                    case "AnimTree":
                        AT.Add(new AnimTree(pcc as ME3Package, i));
                        break;
                    case "AnimSet":
                        AS.Add(new AnimSet(pcc as ME3Package, i));
                        break;
                }
            }
            treeView1.Nodes.Clear();
            foreach (AnimTree at in AT)
                treeView1.Nodes.Add(at.ToTree());
            foreach (AnimSet ans in AS)
                treeView1.Nodes.Add(ans.ToTree());
        }

        private void startScanToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = ME3Directory.cookedPath;
            if (string.IsNullOrEmpty(path))
            {
                MessageBox.Show("This functionality requires ME3 to be installed. Set its path at:\n Options > Set Custom Path > Mass Effect 3");
                return;
            }
            string[] files = Directory.GetFiles(path, "*.pcc");
            filenames = new List<string>();
            int count = 1;
            foreach (string file in files)
            {
                try
                {
                    using (ME3Package _pcc = MEPackageHandler.OpenME3Package(file))
                    {
                        DebugOutput.PrintLn((count++) + "/" + files.Length + " : Scanning file " + Path.GetFileName(file) + " ...");
                        bool found = false;
                        IReadOnlyList<IExportEntry> Exports = _pcc.Exports;
                        foreach (IExportEntry ex in Exports)
                            if (ex.ClassName == "AnimTree" || ex.ClassName == "AnimSet")
                            {
                                DebugOutput.PrintLn("Found Animation!");
                                found = true;
                                break;
                            }
                        if (found)
                            filenames.Add(file); 
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error:\n" + ex.Message);
                    DebugOutput.PrintLn("Could not open " + Path.GetFileName(file));
                }
            }
            RefreshLists();
        }

        public void RefreshLists()
        {
            listBox1.Items.Clear();
            foreach (string s in filenames)
                listBox1.Items.Add(Path.GetFileName(s));
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int n = listBox1.SelectedIndex;
            if (n == -1)
                return;
            LoadPcc(filenames[n]);
        }

        public void WriteString(FileStream fs, string s)
        {
            fs.Write(BitConverter.GetBytes(s.Length), 0, 4);
            fs.Write(GetBytes(s), 0, s.Length);
        }

        public string ReadString(FileStream fs)
        {
            string s = "";
            byte[] buff = new byte[4];
            for (int i = 0; i < 4; i++)
                buff[i] = (byte)fs.ReadByte();
            int count = BitConverter.ToInt32(buff, 0);
            buff = new byte[count];
            for (int i = 0; i < count; i++)
                buff[i] = (byte)fs.ReadByte();
            s = GetString(buff);
            return s;
        }

        public byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
                bytes[i] = (byte)str[i];
            return bytes;
        }

        public string GetString(byte[] bytes)
        {
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
                s += (char)bytes[i];
            return s;
        }

        private void saveDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            d.Filter = "*.dbs|*.dbs";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                FileStream fs = new FileStream(d.FileName, FileMode.Create, FileAccess.Write);
                
                fs.Write(BitConverter.GetBytes(filenames.Count), 0, 4);
                foreach (string s in filenames)
                    WriteString(fs, s);
                fs.Close();
            }
        }

        private void loadDBToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "*.dbs|*.dbs";
            if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                filenames = new List<string>();
                FileStream fs = new FileStream(d.FileName, FileMode.Open, FileAccess.Read);
                
                byte[] buff = new byte[4];
                fs.Read(buff, 0, 4);
                int count = BitConverter.ToInt32(buff, 0);
                for (int i = 0; i < count; i++)
                    filenames.Add(ReadString(fs));
                fs.Close();
                RefreshLists();
            }
        }
        
        private void exportToPSAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode n = treeView1.SelectedNode;
            if (n == null)
                return;
            int idx = GetRootIndex(n) - AT.Count();
            if (idx >= 0 && idx < AS.Count)
            {
                AnimSet ans = AS[idx];
                SaveFileDialog d = new SaveFileDialog();
                d.Filter = "*.psa|*.psa";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ans.ExportToPSA(d.FileName);
                    MessageBox.Show("Done.");
                }
            }
        }

        public int GetRootIndex(TreeNode t)
        {
            if (t.Parent == null)
                return t.Index;
            else
                return GetRootIndex(t.Parent);
        }

        private void importFromPSAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TreeNode n = treeView1.SelectedNode;
            if (n == null)
                return;
            int idx = GetRootIndex(n) - AT.Count();
            if (idx >= 0 && idx < AS.Count)
            {
                AnimSet ans = AS[idx];
                OpenFileDialog d = new OpenFileDialog();
                d.Filter = "*.psa|*.psa";
                if (d.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    if(ans.ImportFromPSA(d.FileName))
                        MessageBox.Show("Done.");
                }
            }
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            IEnumerable<PackageUpdate> relevantUpdates = updates.Where(x => x.change != PackageChange.Import &&
                                                                            x.change != PackageChange.ImportAdd &&
                                                                            x.change != PackageChange.Names);
            if (relevantUpdates.Count() > 0)
            {
                reScan();
            }
        }
    }
}
