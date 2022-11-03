using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Image = System.Drawing.Image;

namespace LSB_Forms
{
    public class PixelWrapper
    {
        public int X { get; set; }
        public int Y { get; set; }

        public BitArray R { get; set; }
        public BitArray G { get; set; }
        public BitArray B { get; set; }

        public IEnumerator<BitArray> GetEnumerator()
        {
            yield return R;
            yield return G;
            yield return B;
        }

        public int Count()
        {
            var counter = 0;
            foreach (var _ in this)
            {
                counter++;
            }
            return counter;
        }

        public static byte ConvertToByte(BitArray bits)
        {
            if (bits.Count != 8)
            {
                throw new ArgumentException("Exception while converting bits to byte.");
            }
            byte[] bytes = new byte[1];
            bits.CopyTo(bytes, 0);
            return bytes[0];
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static string _stopWord = "$over$";
        private static string _secret = "";
        private static Bitmap _bmpBitmap = null!;
        private static string _finalPath = "C:\\Users\\wolod\\Desktop\\NULP\\Методи стенографії та стеганографічного аналізу\\bmp\\Encoded";

        public MainWindow()
        {
            InitializeComponent();
            Decoded_Text_Textbox.IsEnabled = false;
        }

            private void Upload_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fd = new OpenFileDialog();
            if (fd.ShowDialog() == true)
            {
                _bmpBitmap = new Bitmap(Image.FromFile(fd.FileName));
                BmpImage.Source = new BitmapImage(new Uri(fd.FileName));
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if(_bmpBitmap == null || string.IsNullOrWhiteSpace(SecretTextBox.Text))
            {
                ErrorMessage.Text = "Image or Secret hasn't been specified";
                return;
            }

            ErrorMessage.Text = "";
            SecretTextBox.IsEnabled = false;
            UploadButton.IsEnabled = false;


            try
            {
                _secret = SecretTextBox.Text + _stopWord;
                var secretBytes = Encoding.ASCII.GetBytes(_secret);
                var secretBitArray = new BitArray(secretBytes);
                var pixelsTakes = (int)Math.Ceiling((decimal)secretBitArray.Length / 3);

                if(_bmpBitmap.Height * _bmpBitmap.Width < pixelsTakes)
                {
                    throw new ArgumentException("Image too small for current secret message. Please, chose a bigger one.");
                }

                var pixelCounter = 0;
                var pixels = new List<PixelWrapper>(pixelsTakes);

                // Retrieving only that pixels that will be used further
                for (int i = 0; i < _bmpBitmap.Width; i++)
                {
                    for (int j = 0; j < _bmpBitmap.Height; j++)
                    {
                        if (pixelCounter < pixelsTakes)
                        {
                            var pixel = _bmpBitmap.GetPixel(i, j);

                            var R = new BitArray(new byte[] { pixel.R });
                            var G = new BitArray(new byte[] { pixel.G });
                            var B = new BitArray(new byte[] { pixel.B });

                            pixels.Add(new PixelWrapper()
                            {
                                X = i,
                                Y = j,

                                R = R,
                                G = G,
                                B = B
                            });

                            pixelCounter++;
                        }
                    }
                }
                 
                // Embedding secret message
                var iterator = 0;
                var filledCounter = 0;
                while(iterator < secretBitArray.Length - 1)
                {
                    var pixel = pixels[filledCounter];

                    var counter = 0;
                    foreach (var rgbItem in pixel)
                    {
                        // +3 each time -> check if threshold is reached
                        if(secretBitArray.Length - 1 < (iterator + counter))
                        {
                            goto EmbeddingFinished;
                        }
                        // Changing LSB for each r, g and b of a specific pixel by passing secret bits one by one
                        rgbItem[^1] = secretBitArray[iterator + counter];
                        counter++;
                    }

                    iterator += pixel.Count();
                    filledCounter++;
                }

                EmbeddingFinished: 

                // Saving encoded Image
                foreach (var pixel in pixels)
                {
                    var byteR = PixelWrapper.ConvertToByte(pixel.R);
                    var byteG = PixelWrapper.ConvertToByte(pixel.G);
                    var byteB = PixelWrapper.ConvertToByte(pixel.B);

                    _bmpBitmap.SetPixel(pixel.X, pixel.Y, Color.FromArgb(byteR, byteG, byteB));
                }

                _bmpBitmap.Save($"{_finalPath}\\secret-embedded-{DateTime.Now.Millisecond}.bmp", ImageFormat.Bmp);

                SecretTextBox.IsEnabled = true;
                UploadButton.IsEnabled = true;
                Status_TextBox.Text = "Secret message has been embedded successfully !";
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = ex.Message;
                UploadButton.IsEnabled = true;
                Status_TextBox.Text = "Error";
                return;
            }
        }

        private void Decode_Click(object sender, RoutedEventArgs e)
        {
            if (_bmpBitmap == null)
            {
                ErrorMessage.Text = "Image hasn't been uploaded";
                return;
            }

            ErrorMessage.Text = "";

            try
            {
                var decodeStorage = new List<bool>();
                StringBuilder builder = new StringBuilder();

                var skip = 0;
                for (int i = 0; i < _bmpBitmap.Width; i++)
                {
                    for (int j = 0; j < _bmpBitmap.Height; j++)
                    {
                        var pixel = _bmpBitmap.GetPixel(i, j);
                            
                        var R = new BitArray(new byte[] { pixel.R });
                        var G = new BitArray(new byte[] { pixel.G });
                        var B = new BitArray(new byte[] { pixel.B });

                        var encodeR = R[^1];
                        var encodeG = G[^1];
                        var encodeB = B[^1];

                        decodeStorage.AddRange(new[] { encodeR, encodeG, encodeB });

                        if (decodeStorage.Count > 8 + skip)
                        {
                            var binaryCharacter = decodeStorage.Skip(skip).Take(8).Reverse();
                            var bitCharacter = new BitArray(binaryCharacter.ToArray());
                            var bytesCharacter = ToByteArray(bitCharacter);
                            var character = Encoding.ASCII.GetString(bytesCharacter);

                            builder.Append(character);

                            var builtSentece = builder.ToString();

                            if (builtSentece.Contains(_stopWord))
                                goto Break;

                            skip += 8;
                        } 
                    }
                }

            Break:

                var decodedSecret = builder.ToString();
                Decoded_Text_Textbox.Text = decodedSecret.Substring(0, decodedSecret.Length - _stopWord.Length);
                Status_TextBox.Text = "Secret message has been decoded successfully !";
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = ex.Message;
                UploadButton.IsEnabled = true;
                SecretTextBox.IsEnabled = true;
                Status_TextBox.Text = "Error";
                return;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            SecretTextBox.Text = "";
            ErrorMessage.Text = "";
            Decoded_Text_Textbox.Text = "";
            Status_TextBox.Text = "";
            _bmpBitmap = null!;
            BmpImage.Source = null;
            UploadButton.IsEnabled = true;
            SecretTextBox.IsEnabled = true;
        }

        private byte[] ToByteArray(BitArray bits)
        {
            int numBytes = bits.Count / 8;
            if (bits.Count % 8 != 0) numBytes++;

            byte[] bytes = new byte[numBytes];
            int byteIndex = 0, bitIndex = 0;

            for (int i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                    bytes[byteIndex] |= (byte)(1 << (7 - bitIndex));

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }

            return bytes;
        }

        private void Reverse(BitArray array)
        {
            int length = array.Length;
            int mid = (length / 2);

            for (int i = 0; i < mid; i++)
            {
                bool bit = array[i];
                array[i] = array[length - i - 1];
                array[length - i - 1] = bit;
            }
        }
    }
}