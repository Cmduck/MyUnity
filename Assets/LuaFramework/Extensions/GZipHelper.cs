using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using UnityEngine;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;

public class GZipHelper
{
    /// <summary>
    /// 解压GZip
    /// </summary>
    /// <param name="zipname">压缩文件</param>
    /// <param name="unzipname">解压文件</param>
    public static void uncompress(in string zipname, in string unzipname)
    {
        byte[] data = File.ReadAllBytes(zipname);

        uncompressBuffer(data, unzipname);
    }

    public static void compress(in string unzipname, in string zipname)
    {
        byte[] data = File.ReadAllBytes(unzipname);
        compressBuffer(data, zipname);
    }

    public static void compressBuffer(in byte[] buffer, in string output)
    {
        // Use a 4K buffer. Any larger is a waste.    
        byte[] dataBuffer = new byte[4096];

        MemoryStream ms = new MemoryStream(buffer);

        using (FileStream fsOut = File.Create(output))
        {
            using (GZipOutputStream gzipStream = new GZipOutputStream(fsOut))
            {
                StreamUtils.Copy(ms, gzipStream, dataBuffer);
            }
        }
    }

    public static void uncompressBuffer(in byte[] buffer, in string output)
    {
        byte[] dataBuffer = new byte[4096];

        MemoryStream ms = new MemoryStream(buffer);

        // 字节流 => GZipInputStream => FileStream
        using (GZipInputStream gzipStream = new GZipInputStream(ms))
        {
            using (FileStream fsOut = File.Create(output))
            {
                StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
            }
        }

        //FileStream outStream = File.Create(output);
        //GZipOutputStream gzoStream = new GZipOutputStream(outStream);
        //MemoryStream ms = new MemoryStream(buffer);

        //byte[] data = ms.ToArray();
        //gzoStream.Write(data, 0, data.Length);

        //gzoStream.Close();
        //outStream.Dispose();
    }

    public static void compressZip(in string outPathname, in string password, in string folderName)
    {
        using(FileStream fsOut = File.Create(outPathname))
        using(var zipStream = new ZipOutputStream(fsOut)) {

            //0-9, 9 being the highest level of compression
            zipStream.SetLevel(3); 

            // optional. Null is the same as not setting. Required if using AES.
            zipStream.Password = password;	

            // This setting will strip the leading part of the folder path in the entries, 
            // to make the entries relative to the starting folder.
            // To include the full path for each entry up to the drive root, assign to 0.
            int folderOffset = folderName.Length + (folderName.EndsWith("\\") ? 0 : 1);

            compressFolder(folderName, zipStream, folderOffset);
        }
    }

    private static void compressFolder(string path, ZipOutputStream zipStream, int folderOffset)
    {
        var files = Directory.GetFiles(path);

        foreach (var filename in files) {

            var fi = new FileInfo(filename);

            // Make the name in zip based on the folder
            var entryName = filename.Substring(folderOffset);

            // Remove drive from name and fix slash direction
            entryName = ZipEntry.CleanName(entryName); 

            var newEntry = new ZipEntry(entryName);

            // Note the zip format stores 2 second granularity
            newEntry.DateTime = fi.LastWriteTime; 

            // Specifying the AESKeySize triggers AES encryption. 
            // Allowable values are 0 (off), 128 or 256.
            // A password on the ZipOutputStream is required if using AES.
            //   newEntry.AESKeySize = 256;

            // To permit the zip to be unpacked by built-in extractor in WinXP and Server2003,
            // WinZip 8, Java, and other older code, you need to do one of the following: 
            // Specify UseZip64.Off, or set the Size.
            // If the file may be bigger than 4GB, or you do not need WinXP built-in compatibility, 
            // you do not need either, but the zip will be in Zip64 format which
            // not all utilities can understand.
            //   zipStream.UseZip64 = UseZip64.Off;
            newEntry.Size = fi.Length;

            zipStream.PutNextEntry(newEntry);

            // Zip the file in buffered chunks
            // the "using" will close the stream even if an exception occurs
            var buffer = new byte[4096];
            using (FileStream fsInput = File.OpenRead(filename)) {
                StreamUtils.Copy(fsInput, zipStream, buffer);
            }
            zipStream.CloseEntry();
        }
    }

    public static void uncompressZip(string archivePath, string password, string outFolder) {

        using(var fsInput = File.OpenRead(archivePath)) 
        using(var zf = new ZipFile(fsInput)){
            
            if (!string.IsNullOrEmpty(password)) {
                // AES encrypted entries are handled automatically
                zf.Password = password;
            }

            foreach (ZipEntry zipEntry in zf) {
                if (!zipEntry.IsFile) {
                    // Ignore directories
                    continue;
                }
                string entryFileName = zipEntry.Name;
                // to remove the folder from the entry:
                //entryFileName = Path.GetFileName(entryFileName);
                // Optionally match entrynames against a selection list here
                // to skip as desired.
                // The unpacked length is available in the zipEntry.Size property.

                // Manipulate the output filename here as desired.
                var fullZipToPath = Path.Combine(outFolder, entryFileName);
                var directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName.Length > 0) {
                    Directory.CreateDirectory(directoryName);
                }

                // 4K is optimum
                var buffer = new byte[4096];

                // Unzip file in buffered chunks. This is just as fast as unpacking
                // to a buffer the full size of the file, but does not waste memory.
                // The "using" will close the stream even if an exception occurs.
                using(var zipStream = zf.GetInputStream(zipEntry))
                using (Stream fsOutput = File.Create(fullZipToPath)) {
                    StreamUtils.Copy(zipStream, fsOutput , buffer);
                }
            }
        }
    }
}
