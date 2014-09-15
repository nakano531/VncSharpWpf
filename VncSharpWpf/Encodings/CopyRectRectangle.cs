// VncSharpWPF - .NET VNC Client for WPF Library
// Copyright (C) 2008 David Humphrey
// Copyright (C) 2011 Masanori Nakano (Modified VncSharp for WPF)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using System.Windows;

namespace VncSharpWpf.Encodings
{
	/// <summary>
	/// Implementation of CopyRect encoding, as well as drawing support. See RFB Protocol document v. 3.8 section 6.5.2.
	/// </summary>
	public sealed class CopyRectRectangle : EncodedRectangle 
	{
		public CopyRectRectangle(RfbProtocol rfb, Framebuffer framebuffer, Rectangle rectangle)
			: base(rfb, framebuffer, rectangle, RfbProtocol.COPYRECT_ENCODING) 
		{
		}

		// CopyRect Source Point (x,y) from which to copy pixels in Draw
		System.Drawing.Point source;

		/// <summary>
		/// Decodes a CopyRect encoded rectangle.
		/// </summary>
		public override void Decode()
		{
			// Read the source point from which to begin copying pixels
			source = new System.Drawing.Point();
			source.X = (int) rfb.ReadUInt16();
			source.Y = (int) rfb.ReadUInt16();
		}

        public unsafe override void Draw(WriteableBitmap desktop)
        {
            // Avoid exception if window is dragged bottom of screen
            if (rectangle.Top + rectangle.Height >= framebuffer.Height)
            {
                rectangle.Height = framebuffer.Height - rectangle.Top - 1;
            }

            if ((rectangle.Y * desktop.PixelWidth + rectangle.X) < (source.Y * desktop.PixelWidth + source.X))
            {
                Int32Rect srcRect = new Int32Rect(source.X, source.Y, rectangle.Width, rectangle.Height);
                desktop.WritePixels(srcRect, desktop.BackBuffer, desktop.BackBufferStride * desktop.PixelHeight, desktop.PixelWidth * 4, rectangle.X, rectangle.Y);
            }
            else
            {
                Int32[] pixelBuf = new Int32[rectangle.Width * rectangle.Height];

                desktop.CopyPixels(new Int32Rect(source.X, source.Y, rectangle.Width, rectangle.Height), pixelBuf, rectangle.Width * 4, 0);
                desktop.WritePixels(new Int32Rect(0, 0, rectangle.Width, rectangle.Height), pixelBuf, rectangle.Width * 4, rectangle.X, rectangle.Y);
            }
        }
	}
}