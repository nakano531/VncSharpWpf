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

namespace VncSharpWpf
{
    /// <summary>
    /// PasswordDialogWpf.xaml の相互作用ロジック
    /// </summary>
    public partial class PasswordDialogWpf : Window
    {
        public PasswordDialogWpf()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the Password entered by the user.
        /// </summary>
        public string Password
        {
            get
            {
                return this.passwordBox.Password;
            }
        }

        /// <summary>
        /// Creates an instance of PasswordDialog and uses it to obtain the user's password.
        /// </summary>
        /// <returns>Returns the user's password as entered, or null if he/she clicked Cancel.</returns>
        public static string GetPassword()
        {
            PasswordDialogWpf dialog = new PasswordDialogWpf();

            dialog.passwordBox.Focus();
            dialog.ShowDialog();

            if (dialog.DialogResult == true)
            {
                return dialog.passwordBox.Password;
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

        private void PasswordBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (this.passwordBox.Password.Length > 0)
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
