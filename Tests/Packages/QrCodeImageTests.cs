using System;
using System.Linq;
using CourierService.Services.Packages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CourierService.Tests.Packages
{
    [TestClass]
    public class QrCodeImageTests
    {
        // Every PNG file starts with these 8 bytes
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        [TestMethod]
        public void ProducesAPngImage()
        {
            var png = QrCodeImage.Png("F20-0201");

            Assert.IsTrue(png.Length > 100, "Expected a real image, got " + png.Length + " bytes");
            CollectionAssert.AreEqual(PngSignature, png.Take(8).ToArray());
        }

        [TestMethod]
        public void DifferentIdentifiers_GiveDifferentImages()
        {
            CollectionAssert.AreNotEqual(QrCodeImage.Png("F20-0201"), QrCodeImage.Png("F20-0202"));
        }

        [TestMethod]
        public void SameIdentifier_GivesTheSameImage()
        {
            CollectionAssert.AreEqual(QrCodeImage.Png("F20-0201"), QrCodeImage.Png("F20-0201"));
        }

        [TestMethod]
        public void BlankIdentifier_IsRejected()
        {
            Assert.ThrowsExactly<ArgumentException>(() => QrCodeImage.Png(" "));
        }
    }
}