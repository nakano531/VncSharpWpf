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
using System.Threading;
using System.Reflection;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Windows.Controls;

//using VncSharpWpf.Encodings;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Collections.Generic;
using System.Windows;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Globalization;
using System.Net;

namespace VncSharpWpf
{
	/// <summary>
	/// Event Handler delegate declaration used by events that signal successful connection with the server.
	/// </summary>
	public delegate void ConnectCompleteHandler(object sender, ConnectEventArgs e);
	
	/// <summary>
	/// When connecting to a VNC Host, a password will sometimes be required.  Therefore a password must be obtained from the user.  A default Password dialog box is included and will be used unless users of the control provide their own Authenticate delegate function for the task.  For example, this might pull a password from a configuration file of some type instead of prompting the user.
	/// </summary>
	public delegate string AuthenticateDelegate();

    /// <summary>
	/// SpecialKeys is a list of the various keyboard combinations that overlap with the client-side and make it
	/// difficult to send remotely.  These values are used in conjunction with the SendSpecialKeys method.
	/// </summary>
	public enum SpecialKeys {
		CtrlAltDel,
		AltF4,
		CtrlEsc, 
		Ctrl,
		Alt
	}

	[ToolboxBitmap(typeof(RemoteDesktopWpf), "Resources.vncviewer.ico")]
	/// <summary>
	/// The RemoteDesktop control takes care of all the necessary RFB Protocol and GUI handling, including mouse and keyboard support, as well as requesting and processing screen updates from the remote VNC host.  Most users will choose to use the RemoteDesktop control alone and not use any of the other protocol classes directly.
	/// </summary>
	public partial class RemoteDesktopWpf : UserControl
	{
		[Description("Raised after a successful call to the Connect() method.")]
		/// <summary>
		/// Raised after a successful call to the Connect() method.  Includes information for updating the local display in ConnectEventArgs.
		/// </summary>
		public event ConnectCompleteHandler ConnectComplete;
		
		[Description("Raised when the VNC Host drops the connection.")]
		/// <summary>
		/// Raised when the VNC Host drops the connection.
		/// </summary>
		public event EventHandler	ConnectionLost;

        [Description("Raised when the VNC Host sends text to the client's clipboard.")]
        /// <summary>
        /// Raised when the VNC Host sends text to the client's clipboard. 
        /// </summary>
        public event EventHandler   ClipboardChanged;

        public event EventHandler   StoppedListen;
        
		/// <summary>
		/// Points to a Function capable of obtaining a user's password.  By default this means using the PasswordDialog.GetPassword() function; however, users of RemoteDesktop can replace this with any function they like, so long as it matches the delegate type.
		/// </summary>
		public AuthenticateDelegate GetPassword;
		
		WriteableBitmap desktop;                 // Internal representation of remote image.
		VncClient vnc;						     // The Client object handling all protocol-level interaction
		int port = 5900;					     // The port to connectFromClient to on remote host (5900 is default)
		bool passwordPending = false;		     // After Connect() is called, a password might be required.
		bool fullScreenRefresh = false;		     // Whether or not to request the entire remote screen be sent.
        VncDesktopTransformPolicy desktopPolicy;
		RuntimeState state = RuntimeState.Disconnected;

        double imageScale;        // Image Scale

		private enum RuntimeState {
			Disconnected,
			Disconnecting,
			Connected,
			Connecting,
            Listen
		}
		
		public RemoteDesktopWpf() : base()
		{
            InitializeComponent();

			// Use a simple desktop policy for design mode.  This will be replaced in Connect()
            desktopPolicy = new VncDesignModeDesktopPolicy(this);

            if (desktopPolicy.AutoScroll)
            {
                scrollviewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            } else {
                scrollviewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            
			// Users of the control can choose to use their own Authentication GetPassword() method via the delegate above.  This is a default only.
			GetPassword = new AuthenticateDelegate(PasswordDialogWpf.GetPassword);

            // EventHandler Settings
            this.designModeDesktop.SizeChanged +=new System.Windows.SizeChangedEventHandler(SizeChangedEventHandler);
            this.designModeDesktop.MouseMove += new MouseEventHandler(MouseDownUpMoveEventHandler);
            this.designModeDesktop.MouseDown += new MouseButtonEventHandler(MouseDownUpMoveEventHandler);
            this.designModeDesktop.MouseUp += new MouseButtonEventHandler(MouseDownUpMoveEventHandler);
            this.designModeDesktop.MouseWheel +=new MouseWheelEventHandler(MouseWHeelEventHandler);
		}

        /// <summary>
        /// EventHandler for Image Size Change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SizeChangedEventHandler(object sender, RoutedEventArgs e)
        {
            if (IsConnected)
            {
                ImageScale = designModeDesktop.ActualWidth / designModeDesktop.Source.Width;
            }
        }

        /// <summary>
        /// The Image Scale for Mouse Position Convert
        /// </summary>
        public double ImageScale
        {
            get {
                return imageScale;
            }
            set
            {
                imageScale = value;
            }
        }

		[DefaultValue(5900)]
		[Description("The port number used by the VNC Host (typically 5900)")]
		/// <summary>
		/// The port number used by the VNC Host (typically 5900).
		/// </summary>
		public int VncPort {
			get { 
				return port; 
			}
			set { 
				// Ignore attempts to use invalid port numbers
				if (value < 1 | value > 65535) value = 5900;
				port = value;	
			}
		}

		/// <summary>
		/// True if the RemoteDesktop is connected and authenticated (if necessary) with a remote VNC Host; otherwise False.
		/// </summary>
		public bool IsConnected {
			get {
				return state == RuntimeState.Connected;
			}
		}

        public bool IsListen
        {
            get
            {
                return state == RuntimeState.Listen;
            }
        }
		
		// This is a hack to get around the issue of DesignMode returning
		// false when the control is being removed from a form at design time.
		// First check to see if the control is in DesignMode, then work up 
		// to also check any parent controls.  DesignMode returns False sometimes
		// when it is really True for the parent. Thanks to Claes Bergefall for the idea.
		protected bool DesignMode {
			get {
                return System.ComponentModel.DesignerProperties.GetIsInDesignMode(this);
			}
		}

        [Description("The name of the remote desktop.")]
        /// <summary>
        /// The name of the remote desktop, or "Disconnected" if not connected.
        /// </summary>
        public string Hostname {
            get {
                return vnc == null ? "Disconnected" : vnc.HostName;
            }
        }

        /// <summary>
        /// The image of the remote desktop.
        /// </summary>
        public WriteableBitmap Desktop {
            get {
                return desktop;
            }
        }

		/// <summary>
		/// Get a complete update of the entire screen from the remote host.
		/// </summary>
		/// <remarks>You should allow users to call FullScreenUpdate in order to correct
		/// corruption of the local image.  This will simply request that the next update be
		/// for the full screen, and not a portion of it.  It will not do the update while
		/// blocking.
		/// </remarks>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not in the Connected state.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
		public void FullScreenUpdate()
		{
			InsureConnection(true);
			fullScreenRefresh = true;
		}

		/// <summary>
		/// Insures the state of the connection to the server, either Connected or Not Connected depending on the value of the connected argument.
		/// </summary>
		/// <param name="connected">True if the connection must be established, otherwise False.</param>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is in the wrong state.</exception>
		private void InsureConnection(bool connected)
		{
			// Grab the name of the calling routine:
			string methodName = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod().Name;
			
			if (connected) {
				System.Diagnostics.Debug.Assert(state == RuntimeState.Connected ||
                                                state == RuntimeState.Disconnecting,  // special case for Disconnect()
                                                string.Format("RemoteDesktop must be in RuntimeState.Connected before calling {0}.", methodName));
				if (state != RuntimeState.Connected && state != RuntimeState.Disconnecting) {
					throw new InvalidOperationException("RemoteDesktop must be in Connected state before calling methods that require an established connection.");
				}
			} else { // disconnected
				System.Diagnostics.Debug.Assert(state == RuntimeState.Disconnected || 
                                                state == RuntimeState.Listen,
												string.Format("RemoteDesktop must be in RuntimeState.Disconnected before calling {0}.", methodName));
                if (state != RuntimeState.Disconnected && state != RuntimeState.Disconnecting && state != RuntimeState.Listen) {
					throw new InvalidOperationException("RemoteDesktop cannot be in Connected state when calling methods that establish a connection.");
				}
			}
		}

		// This event handler deals with Frambebuffer Updates coming from the host. An
		// EncodedRectangle object is passed via the VncEventArgs (actually an IDesktopUpdater
		// object so that *only* Draw() can be called here--Decode() is done elsewhere).
		// The VncClient object handles thread marshalling onto the UI thread.
		protected void VncUpdate(object sender, VncEventArgs e)
		{
            Dispatcher.Invoke(new Action(() => {
                e.DesktopUpdater.Draw(desktop);
            }));

            if (state == RuntimeState.Connected) {
				vnc.RequestScreenUpdate(fullScreenRefresh);
				
				// Make sure the next screen update is incremental
    			fullScreenRefresh = false;
			}
		}

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host)
        {
            // Use Display 0 by default.
            Connect(host, 0);
        }

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <param name="viewOnly">Determines whether mouse and keyboard events will be sent to the host.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host, bool viewOnly)
        {
            // Use Display 0 by default.
            Connect(host, 0, viewOnly);
        }

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <param name="viewOnly">Determines whether mouse and keyboard events will be sent to the host.</param>
        /// <param name="scaled">Determines whether to use desktop scaling or leave it normal and clip.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host, bool viewOnly, bool scaled)
        {
            // Use Display 0 by default.
            Connect(host, 0, viewOnly, scaled);
        }

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <param name="display">The Display number (used on Unix hosts).</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host, int display)
        {
            Connect(host, display, false);
        }

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <param name="display">The Display number (used on Unix hosts).</param>
        /// <param name="viewOnly">Determines whether mouse and keyboard events will be sent to the host.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host, int display, bool viewOnly)
        {
            Connect(host, display, viewOnly, false);
        }

        /// <summary>
        /// Connect to a VNC Host and determine whether or not the server requires a password.
        /// </summary>
        /// <param name="host">The IP Address or Host Name of the VNC Host.</param>
        /// <param name="display">The Display number (used on Unix hosts).</param>
        /// <param name="viewOnly">Determines whether mouse and keyboard events will be sent to the host.</param>
        /// <param name="scaled">Determines whether to use desktop scaling or leave it normal and clip.</param>
        /// <exception cref="System.ArgumentNullException">Thrown if host is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown if display is negative.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
        public void Connect(string host, int display, bool viewOnly, bool scaled)
        {
            // TODO: Should this be done asynchronously so as not to block the UI?  Since an event 
            // indicates the end of the connection, maybe that would be a better design.
            InsureConnection(false);

            if (host == null) throw new ArgumentNullException("host");
            if (display < 0) throw new ArgumentOutOfRangeException("display", display, "Display number must be a positive integer.");

            // Start protocol-level handling and determine whether a password is needed
            vnc = new VncClient();
            vnc.ConnectionLost += new EventHandler(VncClientConnectionLost);
            vnc.ServerCutText += new EventHandler(VncServerCutText);

            passwordPending = vnc.Connect(host, display, VncPort, viewOnly);

            desktopPolicy = new VncWpfDesktopPolicy(vnc, this);
            SetScalingMode(scaled);

            if (passwordPending) {
                // Server needs a password, so call which ever method is refered to by the GetPassword delegate.
                string password = GetPassword();

                if (password == null) {
                    // No password could be obtained (e.g., user clicked Cancel), so stop connecting
                    return;
                } else {
                    Authenticate(password);
                }
            } else {
                // No password needed, so go ahead and Initialize here
                this.waitLabel.Content = "Connecting to VNC host " + host + "(" + port + ") , please wait... ";
                Initialize();
            }
        }

        /// <summary>
        /// Wait for a connection from VNC Server.
        /// </summary>
        /// <param name="host">Hostname or IP Address</param>
        /// <param name="port">Listening Port</param>
        /// <param name="viewOnly">Set true if you use viewonly mode</param>
        /// <param name="scaled">Set true if you use scaled mode</param>
        public void Listen(string host, int port = 5500, bool viewOnly = false, bool scaled = false)
        {
            InsureConnection(false);

            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            vnc = new VncClient();
            vnc.ConnectionLost += new EventHandler(VncClientConnectionLost);
            vnc.ServerCutText += new EventHandler(VncServerCutText);
            vnc.ConnectedFromServer += new VncConnectedFromServerHandler(ConnectedFromServerEventHandler);

            desktopPolicy = new VncWpfDesktopPolicy(vnc, this);
            SetScalingMode(scaled);
            SetState(RuntimeState.Listen);
            vnc.Listen(host, port, viewOnly);

            this.waitLabel.Content = "Wait for a connection at " + Dns.GetHostEntry(host).AddressList[0] + ":" + port;
            this.waitLabel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Event Handler for Event "ConnectedFromServer" 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="authentication"></param>
        public void ConnectedFromServerEventHandler(object sender, bool authentication)
        {
            if (authentication)
            {
                // Server needs a password, so call which ever method is refered to by the GetPassword delegate.
                string password = GetPassword();

                if (password == null)
                {
                    // No password could be obtained (e.g., user clicked Cancel), so stop connecting
                    return;
                }
                else
                {
                    Authenticate(password);
                }
            }
            else
            {
                // No password needed, so go ahead and Initialize here
                Dispatcher.Invoke(new Action(() => {
                    Initialize();
                }));
            }
        }

		/// <summary>
		/// Authenticate with the VNC Host using a user supplied password.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already Connected.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
		/// <exception cref="System.NullReferenceException">Thrown if the password is null.</exception>
		/// <param name="password">The user's password.</param>
		public void Authenticate(string password)
		{
			InsureConnection(false);
			if (!passwordPending) throw new InvalidOperationException("Authentication is only required when Connect() returns True and the VNC Host requires a password.");
			if (password == null) throw new NullReferenceException("password");

			passwordPending = false;  // repeated calls to Authenticate should fail.
			if (vnc.Authenticate(password)) {
				Initialize();
			} else {		
				OnConnectionLost();										
			}	
		}

        /// <summary>
        /// Changes the input mode to view-only or interactive.
        /// </summary>
        /// <param name="viewOnly">True if view-only mode is desired (no mouse/keyboard events will be sent).</param>
        public void SetInputMode(bool viewOnly)
        {
            if (vnc != null && IsConnected)
            {
                vnc.SetInputMode(viewOnly);
            }
        }

        /// <summary>
        /// Set the remote desktop's scaling mode.
        /// </summary>
        /// <param name="scaled">Determines whether to use desktop scaling or leave it normal and clip.</param>
        public void SetScalingMode(bool scaled)
        {
            if (scaled)
            {
                scrollviewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            else
            {
                scrollviewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
        }

		/// <summary>
		/// After protocol-level initialization and connecting is complete, the local GUI objects have to be set-up, and requests for updates to the remote host begun.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is already in the Connected state.  See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>		
		protected void Initialize()
		{
			// Finish protocol handshake with host now that authentication is done.
			InsureConnection(false);
			vnc.Initialize();
			SetState(RuntimeState.Connected);
			
			// Create a buffer on which updated rectangles will be drawn and draw a "please wait..." 
			// message on the buffer for initial display until we start getting rectangles
			SetupDesktop();

            // Set Ket Event Handler
            this.scrollviewer.PreviewKeyDown += new KeyEventHandler(KeyDownEventHandler);
            this.scrollviewer.PreviewKeyUp += new KeyEventHandler(KeyUpEventHandler);
            this.scrollviewer.Focus();

            // Tell the user of this control the necessary info about the desktop in order to setup the display
			OnConnectComplete(new ConnectEventArgs(vnc.Framebuffer.Width,
												   vnc.Framebuffer.Height, 
												   vnc.Framebuffer.DesktopName));

			// Start getting updates from the remote host (vnc.StartUpdates will begin a worker thread).
			vnc.VncUpdate += new VncUpdateHandler(VncUpdate);
			vnc.StartUpdates();
		}

		private void SetState(RuntimeState newState)
		{
			state = newState;
			
			// Set mouse pointer according to new state
			switch (state) {
				case RuntimeState.Connected:
					// Change the cursor to the "vnc" custor--a see-through dot
                    //Cursor = new Cursor(GetType(), "Resources.vnccursor.cur");
                    Dispatcher.Invoke(new Action(() =>
                    {
                        //Cursor = new Cursor("VncSharpWpf.Resources.vnccursor.cur");
                        Cursor = ((TextBlock)this.Resources["VncCursor"]).Cursor;
                    }));
					break;
				// All other states should use the normal cursor.
				case RuntimeState.Disconnected:
				default:
                    Dispatcher.Invoke(new Action(() =>
                    {
                        Cursor = Cursors.Arrow;
                    }));
					break;
			}
		}

		/// <summary>
		/// Creates and initially sets-up the local bitmap that will represent the remote desktop image.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not already in the Connected state. See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
		protected void SetupDesktop()
		{
			InsureConnection(true);

			// Create a new bitmap to cache locally the remote desktop image.  Use the geometry of the
			// remote framebuffer, and 32bpp pixel format (doesn't matter what the server is sending--8,16,
			// or 32--we always draw 32bpp here for efficiency).
			//desktop = new Bitmap(vnc.Framebuffer.Width, vnc.Framebuffer.Height, PixelFormat.Format32bppPArgb);

            List<System.Windows.Media.Color> colors = new List<System.Windows.Media.Color>();
            colors.Add(System.Windows.Media.Colors.Red);
            colors.Add(System.Windows.Media.Colors.Blue);
            colors.Add(System.Windows.Media.Colors.Green);
            BitmapPalette myPalette = new BitmapPalette(colors);

            Dispatcher.Invoke(new Action(() =>
            {
                desktop = new WriteableBitmap(vnc.Framebuffer.Width, vnc.Framebuffer.Height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, myPalette);
                designModeDesktop.Source = desktop;
            }));

			// Draw a "please wait..." message on the local desktop until the first
			// rectangle(s) arrive and overwrite with the desktop image.
			//DrawDesktopMessage("Connecting to VNC host, please wait...");
            this.waitLabel.Visibility = Visibility.Visible;
		}

		/// <summary>
		/// Stops the remote host from sending further updates and disconnects.
		/// </summary>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not already in the Connected state. See <see cref="VncSharpWpf.RemoteDesktop.IsConnected" />.</exception>
		public void Disconnect()
		{
			InsureConnection(true);
			vnc.ConnectionLost -= new EventHandler(VncClientConnectionLost);
            vnc.ServerCutText -= new EventHandler(VncServerCutText);
			vnc.Disconnect();

            this.scrollviewer.PreviewKeyDown -= new KeyEventHandler(KeyDownEventHandler);
            this.scrollviewer.PreviewKeyUp -= new KeyEventHandler(KeyUpEventHandler);

            if (designModeDesktop.Dispatcher.CheckAccess())
            {
                designModeDesktop.Source = null;
                this.waitLabel.Visibility = Visibility.Hidden;
            }
            else
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    designModeDesktop.Source = null;
                    this.waitLabel.Visibility = Visibility.Hidden;
                }));
            }

			SetState(RuntimeState.Disconnected);
			OnConnectionLost();
		}

        /// <summary>
        /// Stop Listening.
        /// </summary>
        public void StopListen()
        {
            InsureConnection(false);
            
            vnc.ConnectionLost -= new EventHandler(VncClientConnectionLost);
            vnc.ServerCutText -= new EventHandler(VncServerCutText);
            vnc.ConnectedFromServer -= new VncConnectedFromServerHandler(ConnectedFromServerEventHandler);
            vnc.StopListen();

            this.scrollviewer.PreviewKeyDown -= new KeyEventHandler(KeyDownEventHandler);
            this.scrollviewer.PreviewKeyUp -= new KeyEventHandler(KeyUpEventHandler);

            this.waitLabel.Visibility = Visibility.Hidden;
            SetState(RuntimeState.Disconnected);

            OnStoppedListen();
        }

        /// <summary>
        /// Fills the remote server's clipboard with the text in the client's clipboard, if any.
        /// </summary>
        public void FillServerClipboard()
        {
            FillServerClipboard(Clipboard.GetText());
        }

        /// <summary>
        /// Fills the remote server's clipboard with text.
        /// </summary>
        /// <param name="text">The text to put in the server's clipboard.</param>
        public void FillServerClipboard(string text)
        {
            vnc.WriteClientCutText(text);
        }

		/// <summary>
		/// RemoteDesktop listens for ConnectionLost events from the VncClient object.
		/// </summary>
		/// <param name="sender">The VncClient object that raised the event.</param>
		/// <param name="e">An empty EventArgs object.</param>
		protected void VncClientConnectionLost(object sender, EventArgs e)
		{
			// If the remote host dies, and there are attempts to write
			// keyboard/mouse/update notifications, this may get called 
			// many times, and from main or worker thread.
			// Guard against this and invoke Disconnect once.
            if (state == RuntimeState.Connected)
            {
                SetState(RuntimeState.Disconnecting);
                Disconnect();
            }
            else if(state == RuntimeState.Listen)
            {
                StopListen();
            }
		}

        // Handle the VncClient ServerCutText event and bubble it up as ClipboardChanged.
        protected void VncServerCutText(object sender, EventArgs e)
        {
            OnClipboardChanged();
        }

        protected void OnClipboardChanged()
        {
            if (ClipboardChanged != null)
                ClipboardChanged(this, EventArgs.Empty);
        }

		/// <summary>
		/// Dispatches the ConnectionLost event if any targets have registered.
		/// </summary>
		/// <param name="e">An EventArgs object.</param>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is in the Connected state.</exception>
		protected void OnConnectionLost()
		{
			if (ConnectionLost != null) {
				ConnectionLost(this, EventArgs.Empty);
			}
		}
		
		/// <summary>
		/// Dispatches the ConnectComplete event if any targets have registered.
		/// </summary>
		/// <param name="e">A ConnectEventArgs object with information about the remote framebuffer's geometry.</param>
		/// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not in the Connected state.</exception>
		protected void OnConnectComplete(ConnectEventArgs e)
		{
			if (ConnectComplete != null) {
				ConnectComplete(this, e);
			}
		}

        protected void OnStoppedListen()
        {
            if (StoppedListen != null)
            {
                StoppedListen(this, EventArgs.Empty);
            }
        }

        private void MouseDownUpMoveEventHandler(object sender, MouseEventArgs e)
        {
            // Only bother if the control is connected.
            if (IsConnected)
            {
                UpdateRemotePointer();
            }
        }

        private void MouseWHeelEventHandler(object sender, MouseWheelEventArgs e)
        {
            if (!DesignMode && IsConnected)
            {
                System.Windows.Point mousePoint = Mouse.GetPosition(designModeDesktop);
                System.Drawing.Point current = new System.Drawing.Point(Convert.ToInt32(mousePoint.X), Convert.ToInt32(mousePoint.Y));

                byte mask = 0;

                // mouse was scrolled forward
                if (e.Delta > 0)
                {
                    mask += 8;
                }
                else if (e.Delta < 0)
                { // mouse was scrolled backwards
                    mask += 16;
                }

                vnc.WritePointerEvent(mask, desktopPolicy.GetMouseMovePoint(current));
            }
        }

	    private void UpdateRemotePointer()
        {
            // HACK: this check insures that while in DesignMode, no messages are sent to a VNC Host
            // (i.e., there won't be one--NullReferenceException)			
            if (!DesignMode && IsConnected)
            {
                System.Windows.Point mousePoint = Mouse.GetPosition(designModeDesktop);
                System.Drawing.Point current = new System.Drawing.Point(Convert.ToInt32(mousePoint.X), Convert.ToInt32(mousePoint.Y));

                byte mask = 0;

                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    mask += 1;
                }

                if (Mouse.MiddleButton == MouseButtonState.Pressed)
                {
                    mask += 2;
                }

                if (Mouse.RightButton == MouseButtonState.Pressed)
                {
                    mask += 4;
                }

                System.Drawing.Point adjusted = desktopPolicy.UpdateRemotePointer(current);
                //if (adjusted.X < 0 || adjusted.Y < 0)
                //    throw new Exception();

                vnc.WritePointerEvent(mask, desktopPolicy.UpdateRemotePointer(current));
            }
        }

        private void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            if (DesignMode || !IsConnected)
            {
                return;
            }

            char ascii = ConvertKeyToAscii(e.Key);

            if (Char.IsLetterOrDigit(ascii) || Char.IsWhiteSpace(ascii) || Char.IsPunctuation(ascii) ||
                ascii == '~' || ascii == '`' || ascii == '<' || ascii == '>' || ascii == '|' ||
                ascii == '=' || ascii == '+' || ascii == '$' || ascii == '^')
            {
                vnc.WriteKeyboardEvent((UInt32)ascii, true);
                vnc.WriteKeyboardEvent((UInt32)ascii, false);
            }
            else if (ascii == '\b')
            {
                UInt32 keyChar = ((UInt32)'\b') | 0x0000FF00;
                vnc.WriteKeyboardEvent(keyChar, true);
                vnc.WriteKeyboardEvent(keyChar, false);
            }
            else
            {
                ManageKeyDownAndKeyUp(e, true);
            }

            e.Handled = true;
        }

        private void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            if (DesignMode || !IsConnected)
            {
                return;
            }

            ManageKeyDownAndKeyUp(e, false);

            e.Handled = true;
        }
          
        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        internal static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[]lpKeyState, 
                                      [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
                                      int cchBuff, uint wFlags, IntPtr dwhkl);

        /// <summary>
        /// Convert Key to Ascii Character.
        /// </summary>
        /// <param name="key"> Source Key </param>
        private char ConvertKeyToAscii(Key key)
        {
            byte[] keyState = new byte[256];
            GetKeyboardState(keyState);

            uint vKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            IntPtr kbLayout = GetKeyboardLayout(0);
            uint scanCode = MapVirtualKeyEx(vKey, 0, kbLayout);

            StringBuilder sb = new StringBuilder();
            int result = ToUnicodeEx(vKey, scanCode, keyState, sb, (int)5, (uint)0, kbLayout);

            switch (result)
            {
                default:
                case -1:
                case 0: 
                    return (char)0; 
                case 1:
                case 2:
                    return sb.ToString()[0];
            }  
        }

        // Thanks to Lionel Cuir, Christian and the other developers at 
        // Aulofee.com for cleaning-up my keyboard code, specifically:
        // ManageKeyDownAndKeyUp, OnKeyPress, OnKeyUp, OnKeyDown.
        private void ManageKeyDownAndKeyUp(KeyEventArgs e, bool isDown)
        {
            UInt32 keyChar;
            bool isProcessed = true;
            switch (e.Key)
            {
                case Key.Tab:       keyChar = 0x0000FF09; break;
                case Key.Enter:     keyChar = 0x0000FF0D; break;
                case Key.Escape:    keyChar = 0x0000FF1B; break;
                case Key.Home:      keyChar = 0x0000FF50; break;
                case Key.Left:      keyChar = 0x0000FF51; break;
                case Key.Up:        keyChar = 0x0000FF52; break;
                case Key.Right:     keyChar = 0x0000FF53; break;
                case Key.Down:      keyChar = 0x0000FF54; break;
                case Key.PageUp:    keyChar = 0x0000FF55; break;
                case Key.PageDown:  keyChar = 0x0000FF56; break;
                case Key.End:       keyChar = 0x0000FF57; break;
                case Key.Insert:    keyChar = 0x0000FF63; break;
                case Key.LeftShift: keyChar = 0x0000FFE1; break;
                case Key.RightShift:keyChar = 0x0000FFE1; break;

                // BUG FIX -- added proper Alt/CTRL support (Edward Cooke)
                case Key.LeftAlt:   keyChar = 0x0000FFE9; break;
                case Key.RightAlt:  keyChar = 0x0000FFE9; break;
                case Key.LeftCtrl:  keyChar = 0x0000FFE3; break;
                case Key.RightCtrl: keyChar = 0x0000FFE4; break;

                case Key.Delete: keyChar = 0x0000FFFF; break;
                case Key.LWin: keyChar = 0x0000FFEB; break;
                case Key.RWin: keyChar = 0x0000FFEC; break;
                case Key.Apps: keyChar = 0x0000FFEE; break;
                case Key.F1:
                case Key.F2:
                case Key.F3:
                case Key.F4:
                case Key.F5:
                case Key.F6:
                case Key.F7:
                case Key.F8:
                case Key.F9:
                case Key.F10:
                case Key.F11:
                case Key.F12:
                    keyChar = 0x0000FFBE + ((UInt32)e.Key - (UInt32)Key.F1);
                    break;
                default:
                    keyChar = 0;
                    isProcessed = false;
                    break;
            }

            if (isProcessed)
            {
                vnc.WriteKeyboardEvent(keyChar, isDown);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Sends a keyboard combination that would otherwise be reserved for the client PC.
        /// </summary>
        /// <param name="keys">SpecialKeys is an enumerated list of supported keyboard combinations.</param>
        /// <remarks>Keyboard combinations are Pressed and then Released, while single keys (e.g., SpecialKeys.Ctrl) are only pressed so that subsequent keys will be modified.</remarks>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not in the Connected state.</exception>
        public void SendSpecialKeys(SpecialKeys keys)
        {
            this.SendSpecialKeys(keys, true);
        }

        /// <summary>
        /// Sends a keyboard combination that would otherwise be reserved for the client PC.
        /// </summary>
        /// <param name="keys">SpecialKeys is an enumerated list of supported keyboard combinations.</param>
        /// <remarks>Keyboard combinations are Pressed and then Released, while single keys (e.g., SpecialKeys.Ctrl) are only pressed so that subsequent keys will be modified.</remarks>
        /// <exception cref="System.InvalidOperationException">Thrown if the RemoteDesktop control is not in the Connected state.</exception>
        public void SendSpecialKeys(SpecialKeys keys, bool release)
        {
            InsureConnection(true);
            // For all of these I am sending the key presses manually instead of calling
            // the keyboard event handlers, as I don't want to propegate the calls up to the 
            // base control class and form.
            switch (keys)
            {
                case SpecialKeys.Ctrl:
                    PressKeys(new uint[] { 0xffe3 }, release);	// CTRL, but don't release
                    break;
                case SpecialKeys.Alt:
                    PressKeys(new uint[] { 0xffe9 }, release);	// ALT, but don't release
                    break;
                case SpecialKeys.CtrlAltDel:
                    PressKeys(new uint[] { 0xffe3, 0xffe9, 0xffff }, release); // CTRL, ALT, DEL
                    break;
                case SpecialKeys.AltF4:
                    PressKeys(new uint[] { 0xffe9, 0xffc1 }, release); // ALT, F4
                    break;
                case SpecialKeys.CtrlEsc:
                    PressKeys(new uint[] { 0xffe3, 0xff1b }, release); // CTRL, ESC
                    break;
                // TODO: are there more I should support???
                default:
                    break;
            }
        }

        /// <summary>
        /// Given a list of keysym values, sends a key press for each, then a release.
        /// </summary>
        /// <param name="keys">An array of keysym values representing keys to press/release.</param>
        /// <param name="release">A boolean indicating whether the keys should be Pressed and then Released.</param>
        private void PressKeys(uint[] keys, bool release)
        {
            System.Diagnostics.Debug.Assert(keys != null, "keys[] cannot be null.");

            for(int i = 0; i < keys.Length; ++i) {
                vnc.WriteKeyboardEvent(keys[i], true);
            }

            if (release) {
                // Walk the keys array backwards in order to release keys in correct order
                for(int i = keys.Length - 1; i >= 0; --i) {
                    vnc.WriteKeyboardEvent(keys[i], false);
                }
            }
        }
	}
}
