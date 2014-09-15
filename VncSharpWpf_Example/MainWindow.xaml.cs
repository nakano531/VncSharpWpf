using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using VncSharpWpf;
using System.Net;

namespace VncSharpWpf_Example
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            rdp.ConnectComplete += new ConnectCompleteHandler(ConnectCompleteEventHandler);
            rdp.ConnectionLost += new EventHandler(ConnectionLostEventHandler);
            rdp.StoppedListen += new EventHandler(ConnectionLostEventHandler);
        }

        private void ConnectCompleteEventHandler(object sender, EventArgs e)
        {
            MenuItem_FullScrennRefresh.IsEnabled = true;
            MenuItem_SendKeys.IsEnabled = true;
            MenuItem_CopyClipBoard.IsEnabled = true;

            MenuItem_Connect.IsEnabled = false;
            MenuItem_DisConnect.IsEnabled = true;
            MenuItem_Listen.IsEnabled = false;
            MenuItem_StopListen.IsEnabled = false;
        }

        private void ConnectionLostEventHandler(object sender, EventArgs e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
            MenuItem_FullScrennRefresh.IsEnabled = false;
            MenuItem_SendKeys.IsEnabled = false;
            MenuItem_CopyClipBoard.IsEnabled = false;

            MenuItem_Connect.IsEnabled = true;
            MenuItem_DisConnect.IsEnabled = false;
            MenuItem_Listen.IsEnabled = true;
            MenuItem_StopListen.IsEnabled = false;
            }));

        }

        private void MenuItem_Connect_Click(object sender, RoutedEventArgs e)
        {
            if (!rdp.IsConnected)
            {
                string vncHost = ConnectDialogWpf.GetVncHost();

                if (vncHost != null)
                {
                    try
                    {
                        rdp.Connect("localhost", MenuItem_ViewOnly.IsChecked, MenuItem_ClippedView.IsChecked);
                    }
                    catch (VncProtocolException vex)
                    {
                        MessageBox.Show(this,
                                        string.Format("Unable to connect to VNC host:\n\n{0}.\n\nCheck that a VNC host is running there.", vex.Message),
                                        string.Format("Unable to Connect to {0}", vncHost),
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Exclamation);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this,
                                        string.Format("Unable to connect to host.  Error was: {0}", ex.Message),
                                        string.Format("Unable to Connect to {0}", vncHost),
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Exclamation);
                    }
                } 
            }
        }

        private void MenuItem_DisConnect_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.Disconnect();

                //MenuItem_Connect.IsEnabled = true;
                //MenuItem_DisConnect.IsEnabled = false;
            }
        }

        private void MenuItem_Listen_Click(object sender, RoutedEventArgs e)
        {
            if (!rdp.IsConnected)
            {
                string selectedAddr = (sender as MenuItem).Header.ToString();

                rdp.Listen(selectedAddr, 5500, MenuItem_ViewOnly.IsChecked, MenuItem_ClippedView.IsChecked);

                MenuItem_Connect.IsEnabled = false;
                MenuItem_DisConnect.IsEnabled = false;
                MenuItem_Listen.IsEnabled = false;
                MenuItem_StopListen.IsEnabled = true;
            }
        }

        private void MenuItem_StopListen_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsListen)
            {
                rdp.StopListen();
            }
        }

        private void MenuItem_CtrlAltDel_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.SendSpecialKeys(SpecialKeys.CtrlAltDel);
            }
        }

        private void MenuItem_AltF4_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.SendSpecialKeys(SpecialKeys.AltF4);
            }
        }

        private void MenuItem_CtrlEsc_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.SendSpecialKeys(SpecialKeys.CtrlEsc);
            }
        }

        private void MenuItem_Ctrl_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.SendSpecialKeys(SpecialKeys.Ctrl);
            }
        }

        private void MenuItem_Alt_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.SendSpecialKeys(SpecialKeys.Alt);
            }
        }

        private void MenuItem_ClippedView_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_ClippedView.IsChecked = true;
            MenuItem_ClippedView.IsEnabled = false;
            MenuItem_ScaledView.IsChecked = false;
            MenuItem_ScaledView.IsEnabled = true;

            rdp.SetScalingMode(true);
        }

        private void MenuItem_CopyClipBoard_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.FillServerClipboard();
            }
        }

        private void MenuItem_ScaledView_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_ClippedView.IsChecked = false;
            MenuItem_ClippedView.IsEnabled = true;
            MenuItem_ScaledView.IsChecked = true;
            MenuItem_ScaledView.IsEnabled = false;

            rdp.SetScalingMode(false);
        }

        private void MenuItem_ViewOnly_Click(object sender, RoutedEventArgs e)
        {
            MenuItem_ViewOnly.IsChecked = !MenuItem_ViewOnly.IsChecked;

            rdp.SetInputMode(MenuItem_ViewOnly.IsChecked);
        }

        private void MenuItem_FullScreenRefresh_Click(object sender, RoutedEventArgs e)
        {
            rdp.FullScreenUpdate();
        }

        private void MenuItem_Quit_Click(object sender, RoutedEventArgs e)
        {
            if (rdp.IsConnected)
            {
                rdp.Disconnect();
            }

            Application.Current.Shutdown();
        }
    }
}
