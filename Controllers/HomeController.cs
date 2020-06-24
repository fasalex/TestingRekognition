using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using CelebrityRekognition.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CelebrityRekognition.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();
            RecognizeCelebritiesRequest recognizeCelebritiesRequest = new RecognizeCelebritiesRequest();

            Image img = new Image();

            var sourceStream = file.OpenReadStream();
            await using (var memoryStream = new MemoryStream())
            {
                await sourceStream.CopyToAsync(memoryStream);
                img.Bytes = memoryStream;
            }
            recognizeCelebritiesRequest.Image = img;
            RecognizeCelebritiesResponse recognizeCelebritiesResponse = await rekognitionClient.RecognizeCelebritiesAsync(recognizeCelebritiesRequest);
            return View("Index", recognizeCelebritiesResponse.CelebrityFaces);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
