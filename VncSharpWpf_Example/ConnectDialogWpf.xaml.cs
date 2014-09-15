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
using System.Windows.Shapes;

namespace VncSharpWpf_Example
{
    /// <summary>
    /// ConnectDialogWpf.xaml の相互作用ロジック
    /// </summary>
    public partial class ConnectDialogWpf : Window
    {
        public ConnectDialogWpf()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the Password entered by the user.
        /// </summary>
        public string VncHost
        {
            get
            {
                return this.VncHostBox.Text;
            }
        }

        /// <summary>
        /// Creates an instance of PasswordDialog and uses it to obtain the user's password.
        /// </summary>
        /// <returns>Returns the user's password as entered, or null if he/she clicked Cancel.</returns>
        public static string GetVncHost()
        {
            ConnectDialogWpf dialog = new ConnectDialogWpf();

            dialog.VncHostBox.Focus();
            dialog.ShowDialog();


            if (dialog.DialogResult == true)
            {
                return dialog.VncHostBox.Text;
            }
            else
            {
                return null;
            }
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void VncHost_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.VncHostBox.Text.Length > 0)
            {
                if (e.Key == Key.Enter)
                {
                    // If Enter Key is Pressed and Password length is not 0, Password is accepted.
                    this.DialogResult = true;
                }
                else
                {
                    this.OK_Button.IsEnabled = true;
                }
            }
            else
            {
                this.OK_Button.IsEnabled = false;
            }
        }
    }
}
