//   SparkleShare, a collaboration and sharing tool.
//   Copyright (C) 2010  Hylke Bons <hylkebons@gmail.com>
//
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//   GNU General Public License for more details.
//
//   You should have received a copy of the GNU General Public License
//   along with this program. If not, see <http://www.gnu.org/licenses/>.


using System;
using System.Threading;

using SparkleLib;

namespace SparkleShare {

    public enum IconState {
        Idle,
        SyncingUp,
        SyncingDown,
        Syncing,
        Error
    }


    public class SparkleStatusIconController {

        public event UpdateIconEventHandler UpdateIconEvent = delegate { };
        public delegate void UpdateIconEventHandler (IconState state);

        public event UpdateMenuEventHandler UpdateMenuEvent = delegate { };
        public delegate void UpdateMenuEventHandler (IconState state);

        public event UpdateStatusItemEventHandler UpdateStatusItemEvent = delegate { };
        public delegate void UpdateStatusItemEventHandler (string state_text);

        public event UpdateQuitItemEventHandler UpdateQuitItemEvent = delegate { };
        public delegate void UpdateQuitItemEventHandler (bool quit_item_enabled);

        public event UpdateRecentEventsItemEventHandler UpdateRecentEventsItemEvent = delegate { };
        public delegate void UpdateRecentEventsItemEventHandler (bool recent_events_item_enabled);

        public IconState CurrentState = IconState.Idle;
        public string StateText = "Welcome to SparkleShare!";

        public readonly int MenuOverflowThreshold   = 9;
        public readonly int MinSubmenuOverflowCount = 3;

        public string [] Folders;
        public string [] FolderErrors;
        
        public string [] OverflowFolders;
        public string [] OverflowFolderErrors;
        

        public string FolderSize {
            get {
                double size = 0;

                foreach (SparkleRepoBase repo in Program.Controller.Repositories)
                    size += repo.Size;

                if (size == 0)
                    return "";
                else
                    return "— " + Program.Controller.FormatSize (size);
            }
        }

        public int ProgressPercentage {
            get {
                return (int) Program.Controller.ProgressPercentage;
            }
        }

        public string ProgressSpeed {
            get {
                return Program.Controller.ProgressSpeed;
            }
        }

        public bool QuitItemEnabled {
            get {
                return (CurrentState == IconState.Idle || CurrentState == IconState.Error);
            }
        }

        public bool RecentEventsItemEnabled {
            get {
                return (Program.Controller.RepositoriesLoaded && Program.Controller.Folders.Count > 0);
            }
        }


        public SparkleStatusIconController ()
        {
            UpdateFolders ();

            Program.Controller.FolderListChanged += delegate {
                if (CurrentState != IconState.Error) {
                    CurrentState = IconState.Idle;

                    if (Program.Controller.Folders.Count == 0)
                        StateText = "Welcome to SparkleShare!";
                    else
                        StateText = "Files up to date " + FolderSize;
                }

                UpdateFolders ();

                UpdateStatusItemEvent (StateText);
                UpdateRecentEventsItemEvent (RecentEventsItemEnabled);
                UpdateMenuEvent (CurrentState);
            };

            Program.Controller.OnIdle += delegate {
                UpdateFolders ();

                if (CurrentState != IconState.Error) {
                    CurrentState = IconState.Idle;

                    if (Program.Controller.Folders.Count == 0)
                        StateText = "Welcome to SparkleShare!";
                    else
                        StateText = "Files up to date " + FolderSize;
                }

                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);
                UpdateIconEvent (CurrentState);
                UpdateMenuEvent (CurrentState);
            };

            Program.Controller.OnSyncing += delegate {
				int repos_syncing_up   = 0;
				int repos_syncing_down = 0;
				
				foreach (SparkleRepoBase repo in Program.Controller.Repositories) {
					if (repo.Status == SyncStatus.SyncUp)
						repos_syncing_up++;
					
					if (repo.Status == SyncStatus.SyncDown)
						repos_syncing_down++;
				}
				
				if (repos_syncing_up > 0 &&
				    repos_syncing_down > 0) {
					
					CurrentState = IconState.Syncing;
                    StateText    = "Syncing changes…";
				
				} else if (repos_syncing_down == 0) {
					CurrentState = IconState.SyncingUp;
                    StateText    = "Sending changes…";
					
				} else {
					CurrentState = IconState.SyncingDown;
                    StateText    = "Receiving changes…";
				}

                StateText += " " + ProgressPercentage + "%  " + ProgressSpeed;

                UpdateIconEvent (CurrentState);
                UpdateStatusItemEvent (StateText);
                UpdateQuitItemEvent (QuitItemEnabled);
            };

            Program.Controller.OnError += delegate {
                CurrentState = IconState.Error;
                StateText    = "Failed to send some changes";

                UpdateFolders ();
                
                UpdateQuitItemEvent (QuitItemEnabled);
                UpdateStatusItemEvent (StateText);
                UpdateIconEvent (CurrentState);
                UpdateMenuEvent (CurrentState);
            };
        }


        public void SparkleShareClicked ()
        {
            Program.Controller.OpenSparkleShareFolder ();
        }


        public void SubfolderClicked (string subfolder)
        {
            Program.Controller.OpenSparkleShareFolder (subfolder);
        }


        public void AddHostedProjectClicked ()
        {
            Program.Controller.ShowSetupWindow (PageType.Add);
        }


        public void RecentEventsClicked ()
        {
            new Thread (() => Program.Controller.ShowEventLogWindow ()).Start ();
        }


        public void AboutClicked ()
        {
            Program.Controller.ShowAboutWindow ();
        }
        
		
        public void QuitClicked ()
        {
            Program.Controller.Quit ();
        }


        private void UpdateFolders ()
        {
            int overflow_count = (Program.Controller.Folders.Count - MenuOverflowThreshold);

            if (overflow_count >= MinSubmenuOverflowCount) {
                Folders = Program.Controller.Folders.GetRange (0, MenuOverflowThreshold).ToArray ();
                OverflowFolders = Program.Controller.Folders.GetRange (MenuOverflowThreshold, overflow_count).ToArray ();
            } else {
                Folders = Program.Controller.Folders.ToArray ();
                OverflowFolders = new string [0];
            }

            string [] errors = new string [Folders.Length];
            string [] overflow_errors = new string [OverflowFolders.Length];
            
            int i = 0;
            foreach (SparkleRepoBase repo in Program.Controller.Repositories) {
                string error_message;

                if (repo.Error == ErrorStatus.HostUnreachable) {
                    error_message = "Host unreachable";
                    
                } else if (repo.Error == ErrorStatus.HostIdentityChanged) {
                    error_message = "Host identity changed";
                    
                } else if (repo.Error == ErrorStatus.AuthenticationFailed) {
                    error_message = "Authentication failed";
                    
                } else if (repo.Error == ErrorStatus.DiskSpaceExcedeed) {
                    error_message = "Out of disk space";
                    
                } else {
                    error_message = "";
                }

                if (i > Folders.Length - 1)
                    overflow_errors [i] = error_message;
                else
                    errors [i] = error_message;

                i++;
            }

            FolderErrors         = errors;
            OverflowFolderErrors = overflow_errors;
        }
    }
}
