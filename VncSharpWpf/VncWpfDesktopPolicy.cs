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
//using System.Windows.Forms;
using System.Drawing;

using VncSharpWpf;

namespace VncSharpWpf
{
	/// <summary>
	/// A clipped version of VncDesktopTransformPolicy.
	/// </summary>
	public sealed class VncWpfDesktopPolicy : VncDesktopTransformPolicy
	{
        public VncWpfDesktopPolicy(VncClient vnc,
                                       RemoteDesktopWpf remoteDesktop) 
            : base(vnc, remoteDesktop)
        {
        }

        public override bool AutoScroll {
            get {
                return true;
            }
        }

        public override Size AutoScrollMinSize {
            get {
                if (vnc != null && vnc.Framebuffer != null) {
                    return new Size(vnc.Framebuffer.Width, vnc.Framebuffer.Height);
                } else {
                    return new Size(100, 100);
                }
            }
        }

        public override Point UpdateRemotePointer(Point current)
        {
            Point adjusted = new Point();

            adjusted.X = (int)((double)current.X / remoteDesktop.ImageScale);
            adjusted.Y = (int)((double)current.Y / remoteDesktop.ImageScale);

            return adjusted;
        }

        public override Rectangle AdjustUpdateRectangle(Rectangle updateRectangle)
        {
			int x, y;


            if (remoteDesktop.ActualWidth > remoteDesktop.designModeDesktop.ActualWidth)
            {
                x = updateRectangle.X + (int)(remoteDesktop.ActualWidth - remoteDesktop.designModeDesktop.ActualWidth) / 2;
            }
            else
            {
                x = updateRectangle.X;
            }

            if (remoteDesktop.ActualHeight > remoteDesktop.designModeDesktop.ActualHeight)
            {
                y = updateRectangle.Y + (int)(remoteDesktop.ActualHeight - remoteDesktop.designModeDesktop.ActualHeight) / 2;
            }
            else
            {
                y = updateRectangle.Y;
            }

			return new Rectangle(x, y, updateRectangle.Width, updateRectangle.Height);
        }

        public override Point GetMouseMovePoint(Point current)
        {
            return UpdateRemotePointer(current);
        }
    }
}