﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CommandLine.Utility;

namespace Snap2HTML
{

    public class PCSDat : IEquatable<PCSDat>
    {
        public int ID { get; set; }
        public bool IsPcsFile { get; set; }

        public string ProjectName { get; set; }
        private string ProjectNum { get; set; }
        private string Revision { get; set; }
        private string LastDate { get; set; }

        public string Customer { get; set; }


        public void PCSDAT()
        {
            this.ID = 0;
            this.IsPcsFile = false;
            this.ProjectName = "";
            this.ProjectNum = "";
            this.Revision = "";
            this.LastDate = "";
            this.Customer = "";
        }


        public PCSDat GetProjectDataFromFile(string sfile)
        {
            StreamReader file = new StreamReader(@sfile);

            string line = file.ReadLine();

            IsPcsFile = false;

            if (!line.StartsWith(".PCsCADProject")) return null;    // praleidziam faila, jai jis ne PCSchematic'o

            IsPcsFile = true;

            while (line != null)
            {
                line = file.ReadLine();

                /* traukiam projekto numeri */
                if (line.StartsWith(".ProText 'Project number'"))
                {
                    ProjectNum = (line.Substring(26)).Replace("\"", "");

                    continue;
                }

                /* traukiam revizija */
                if (line.StartsWith(".ProText Revision"))
                {
                    Revision = (line.Substring(18)).Replace("\"", "");

                    foreach (char c in Revision)
                    {
                        if (!Char.IsDigit(c)) Revision = "";
                    }

                    continue;
                }
                
                /* istraukiam data */
                if (line.StartsWith(".ProText Date"))
                {
                    LastDate = (line.Substring(14)).Replace("\"", "");

                    continue;
                }

                if (line.StartsWith(".ProText 'Customer name'"))
                {
                    Customer = (line.Substring(25)).Replace("\"" , "").Replace("'","").Replace(" ","");

                    continue;
                }


                /* traukiam projekto pavadinima */
                if (line.StartsWith(".ProText 'Project name'"))
                {
                    ProjectName = (line.Substring(25)).Replace("\"", "").Replace("'","");

                    continue;
                }


                if (line.StartsWith(".Database")) break;
            }

            file.Close();

            return this;
        }


        public void ClearProjData()
        {
            ID = 0;
            IsPcsFile = false;
            ProjectNum = "";
            ProjectName = "";
            Revision = "";
            LastDate = "";
            Customer = "";
        }

        public override string ToString()
        {
            // Duomenu struktura:
            // 

            return ( (IsPcsFile == true ) ? 'Y' : 'N') + "|" + ProjectNum + "|" + Revision + "|" + LastDate + "|" + Customer + "|" + ProjectName;
        }

        public bool Equals(PCSDat other)
        {
            if (other == null) return false;
            return (this.ID.Equals(other.ID));
        }
    }


    public partial class frmMain : Form
	{

        private void backgroundWorker_DoWork( object sender, DoWorkEventArgs e )
		{
			backgroundWorker.ReportProgress( 0, "Reading folders..." );
			var sbDirArrays = new StringBuilder();
			int prevDepth = -100;

            StringBuilder strCustomerArray = new StringBuilder();
            PCSDat pcs = new PCSDat();

            string last_write_date = "-";

            // Get all folders
            var dirs = new List<string>();

            dirs.Insert( 0, txtRoot.Text );

            var skipHidden = ( chkHidden.CheckState == CheckState.Unchecked );
			var skipSystem = ( chkSystem.CheckState == CheckState.Unchecked );

            DirSearch( txtRoot.Text, dirs, skipHidden, skipSystem );

            dirs = SortDirList( dirs );

			int totDirs = 0;
			dirs.Add( "*EOF*" );
			long totSize = 0;
			int totFiles = 0;
			var lineBreakSymbol = "\n";	// could set to \n to make html more readable at the expense of increased size

			// Get files in folders
			for( int d = 0; d < dirs.Count; d++ )
			{
				string currentDir = dirs[d];

                try
				{
					int newDepth = currentDir.Split( System.IO.Path.DirectorySeparatorChar ).Length;
					if( currentDir.Length < 64 && currentDir == System.IO.Path.GetPathRoot( currentDir ) ) newDepth--;	// fix reading from rootfolder, <64 to avoid going over MAX_PATH

                    prevDepth = newDepth;

					var sbCurrentDirArrays = new StringBuilder();

                    if ( currentDir != "*EOF*" )
					{
						bool no_problem = true;

                        try
						{
							var files = new List<string>( System.IO.Directory.GetFiles( currentDir, "*.*", System.IO.SearchOption.TopDirectoryOnly ) );
                            //var files = new List<string>(System.IO.Directory.GetFiles(currentDir, "*.*", System.IO.SearchOption.TopDirectoryOnly) .Where(s => !s.Contains("~")) );

                            files.Sort();
							int f = 0;

                            //last_write_date = System.IO.Directory.GetLastWriteTime( currentDir ).ToLocalTime().ToString();

                            long dir_size = 0;

							sbCurrentDirArrays.Append( "D.p([" + lineBreakSymbol );
                            
                            var sDirWithForwardSlash = currentDir.Replace( @"\", "/" );

                            sbCurrentDirArrays.Append( "\"" ).Append( MakeCleanJsString( sDirWithForwardSlash ) ).Append( "|" ).Append( dir_size ).Append( "|" ).Append( last_write_date ).Append( "\"," + lineBreakSymbol );

                            f++;

							long dirSize = 0;

                            pcs = new PCSDat();

                            foreach ( string sFile in files )
							{
								bool bInclude = true;
								long fi_length = 0;
								last_write_date = "-";

                                try
								{
									System.IO.FileInfo fi = new System.IO.FileInfo( sFile );

									if( ( fi.Attributes & System.IO.FileAttributes.Hidden ) == System.IO.FileAttributes.Hidden && chkHidden.CheckState != CheckState.Checked ) bInclude = false;
									if( ( fi.Attributes & System.IO.FileAttributes.System ) == System.IO.FileAttributes.System && chkSystem.CheckState != CheckState.Checked ) bInclude = false;

									fi_length = fi.Length;

									try
									{
										last_write_date = fi.LastWriteTime.ToLocalTime().ToString();

                                        string ext = fi.Extension;

                                        pcs.GetProjectDataFromFile(sFile);  // tikrinam, ar tai PCS failas; jai taip, - istraukiam info apie projekta

                                        if (pcs.IsPcsFile == true)
                                        {
                                            string[] custList = pcs.Customer.Split(';');

                                            foreach(string custname in custList)
                                            {
                                                if (!strCustomerArray.ToString().Contains(custname))
                                                {
                                                    if (strCustomerArray.Length > 0) strCustomerArray.Append("|");

                                                    strCustomerArray.Append(custname);
                                                }
                                            }
                                        }
                                    }
									catch( Exception ex )
									{
										Console.WriteLine( "{0} Exception caught.", ex );
									}
								}
								catch( Exception ex )
								{
									Console.WriteLine( "{0} Exception caught.", ex );
									bInclude = false;
								}

								if( bInclude )
								{
                                    
                                    sbCurrentDirArrays.Append( "\"" ).Append( MakeCleanJsString( System.IO.Path.GetFileName( sFile ) ) ).Append( "|" ).Append( fi_length ).Append( "|" ).Append( last_write_date ).Append("|").Append(pcs.ToString()).Append( "\"," + lineBreakSymbol );
									totSize += fi_length;
									dirSize += fi_length;
									totFiles++;
									f++;

                                    pcs.ClearProjData();

									//if( totFiles % 9 == 0 )
									//{
										backgroundWorker.ReportProgress( 0, "Reading files... " + totFiles + " (" + sFile + ")" );
									//}

								}
								if( backgroundWorker.CancellationPending )
								{
									backgroundWorker.ReportProgress( 0, "Operation Cancelled!" );
									return;
								}
							}

							// Add total dir size
							sbCurrentDirArrays.Append( "" ).Append( dirSize ).Append( "," );

							// Add subfolders
							string subdirs = "";

							List<string> lstSubDirs = new List<string>( System.IO.Directory.GetDirectories( currentDir ) );

							lstSubDirs = SortDirList( lstSubDirs );

							foreach( string sTmp in lstSubDirs )
							{
								int i = dirs.IndexOf( sTmp );
								if( i != -1 ) subdirs += i + "|";
							}

							if( subdirs.EndsWith( "|" ) ) subdirs = subdirs.Remove( subdirs.Length - 1 );

							sbCurrentDirArrays.Append( "\"" ).Append( subdirs ).Append( "\"" + lineBreakSymbol );	// subdirs
							sbCurrentDirArrays.Append( "])" );
							sbCurrentDirArrays.Append( "\n" );
                        }
						catch( Exception ex )
						{
							Console.WriteLine( "{0} Exception caught.", ex );
							no_problem = false;
						}

						if( no_problem == false )	// We need to keep folder even if error occurred for integrity
						{
							var sDirWithForwardSlash = currentDir.Replace( @"\", "/" );

							sbCurrentDirArrays = new StringBuilder();

							sbCurrentDirArrays.Append( "D.p([\"" ).Append( MakeCleanJsString( sDirWithForwardSlash ) ).Append( "*0*-\"," + lineBreakSymbol );
							sbCurrentDirArrays.Append( "0," + lineBreakSymbol );	// total dir size
							sbCurrentDirArrays.Append( "\"\"" + lineBreakSymbol );	// subdirs
							sbCurrentDirArrays.Append( "])" + lineBreakSymbol );
							no_problem = true;
						}

						if( no_problem )
						{
							sbDirArrays.Append( sbCurrentDirArrays.ToString() );
							totDirs++;
						}
					}

				}
				catch( System.Exception ex )
				{
					Console.WriteLine( "{0} exception caught: {1}", ex, ex.Message );
				}

			}


            if (backgroundWorker.CancellationPending)
            {
                backgroundWorker.ReportProgress(0, "User cancelled");
                return;
            }

            // -- Generate Output --
            backgroundWorker.ReportProgress( 0, "Generating HTML file..." );

			// Read template
			var sbContent = new StringBuilder();
			try
			{
				using( System.IO.StreamReader reader = new System.IO.StreamReader( System.IO.Path.GetDirectoryName( Application.ExecutablePath ) + System.IO.Path.DirectorySeparatorChar + "template.html" ) )
				{
					sbContent.Append( reader.ReadToEnd() );
				}
			}
			catch( System.Exception ex )
			{
				MessageBox.Show( "Failed to open 'Template.html' for reading...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				backgroundWorker.ReportProgress( 0, "An error occurred..." );
				return;
			}

            last_write_date = DateTime.Now.ToString();


            // Build HTML
            sbContent.Replace("[LAST_TIME]", last_write_date);
            sbContent.Replace( "[DIR DATA]", sbDirArrays.ToString() );
            sbContent.Replace( "[TITLE]", txtTitle.Text );
			sbContent.Replace( "[APP LINK]", "http://www.rlvision.com" );
			sbContent.Replace( "[APP NAME]", Application.ProductName );
			sbContent.Replace( "[APP VER]", Application.ProductVersion.Split( '.' )[0] + "." + Application.ProductVersion.Split( '.' )[1] );
			sbContent.Replace( "[GEN TIME]", DateTime.Now.ToString( "t" ) );
			sbContent.Replace( "[GEN DATE]", DateTime.Now.ToString( "d" ) );
			sbContent.Replace( "[NUM FILES]", totFiles.ToString() );
			sbContent.Replace( "[NUM DIRS]", totDirs.ToString() );
			sbContent.Replace( "[TOT SIZE]", totSize.ToString() );
			if( chkLinkFiles.Checked )
			{
				sbContent.Replace( "[LINK FILES]", "true" );
				sbContent.Replace( "[LINK ROOT]", txtLinkRoot.Text.Replace( @"\", "/" ) );
				sbContent.Replace( "[SOURCE ROOT]", txtRoot.Text.Replace( @"\", "/" ) );

				string link_root = txtLinkRoot.Text.Replace( @"\", "/" );
				if( IsWildcardMatch( @"?:/*", link_root, false ) )  // "file://" is needed in the browser if path begins with drive letter, else it should not be used
				{
					sbContent.Replace( "[LINK PROTOCOL]", @"file://" );
				}
				else
				{
					sbContent.Replace( "[LINK PROTOCOL]", "" );
				}
			}
			else
			{
				sbContent.Replace( "[LINK FILES]", "false" );
				sbContent.Replace( "[LINK PROTOCOL]", "" );
				sbContent.Replace( "[LINK ROOT]", "" );
				sbContent.Replace( "[SOURCE ROOT]", txtRoot.Text.Replace( @"\", "/" ) );
			}

            sbContent.Replace("[PCS_CUSTOMERS]", "CUST.p([\"" + strCustomerArray.ToString() + "\"])\n");




            // Write output file
            try
			{
				using( System.IO.StreamWriter writer = new System.IO.StreamWriter( saveFileDialog1.FileName ) )
				{
					writer.Write( sbContent.ToString() );
				}

				if( chkOpenOutput.Checked == true )
				{
					System.Diagnostics.Process.Start( saveFileDialog1.FileName );
				}
			}
			catch( System.Exception excpt )
			{
				MessageBox.Show( "Failed to open file for writing:\n\n" + excpt, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
				backgroundWorker.ReportProgress( 0, "An error occurred..." );
				return;
			}

			// Ready!
			Cursor.Current = Cursors.Default;
			backgroundWorker.ReportProgress( 100, "Ready!" );
		}

		private void backgroundWorker_ProgressChanged( object sender, ProgressChangedEventArgs e )
		{
			toolStripStatusLabel1.Text = e.UserState.ToString();
		}

		private void backgroundWorker_RunWorkerCompleted( object sender, RunWorkerCompletedEventArgs e )
		{
			Cursor.Current = Cursors.Default;
			tabControl1.Enabled = true;
			this.Text = "Snap2HTML";

			// Quit when finished if automated via command line
			if( outFile != "" )
			{
				Application.Exit();
			}
		}
	}
}
