using Microsoft.JSInterop;
using AgentToolComponents.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgentToolComponents
{
    public class DownloadFilePlugin(DownloadFileService downloadFileService)
    {
        [Description("Download a file")]
        public async Task<string> DownloadFile([Description("Name of file to download. Must include file extension.")] string fileName, [Description("Content of the file to download")] string fileText)
        {
            try
            {
                await downloadFileService.DownloadFileAsync(fileName, Encoding.UTF8.GetBytes(fileText));
                return "success";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download file failed.\n{ex}");
                return $"download failed. Reason: {ex.Message}";
            }
        }
        
    }
}
