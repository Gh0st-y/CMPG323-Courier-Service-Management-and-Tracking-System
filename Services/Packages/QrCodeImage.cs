using System;
using QRCoder;

namespace CourierService.Services.Packages
{
    /// <summary>
    /// Draws the QR code for a package label (CON-008). The code holds the F20 identifier and nothing else, so a
    /// lost label gives away no personal details, and the scan screen can look the package up from it.
    /// </summary>
    public static class QrCodeImage
    {
        // 10 pixels per module gives roughly a 250 to 330 pixel image, which prints sharply on a label
        public const int PixelsPerModule = 10;

        public static byte[] Png(string f20Identifier)
        {
            if (string.IsNullOrWhiteSpace(f20Identifier))
            {
                throw new ArgumentException("An identifier is required.", nameof(f20Identifier));
            }

            // Medium error correction: still scans if about 15% of the code is scratched or smudged
            using (var generator = new QRCodeGenerator())
            using (var data = generator.CreateQrCode(f20Identifier.Trim(), QRCodeGenerator.ECCLevel.M))
            {
                return new PngByteQRCode(data).GetGraphic(PixelsPerModule);
            }
        }
    }
}