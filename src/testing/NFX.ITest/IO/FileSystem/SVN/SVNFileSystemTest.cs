﻿/*<FILE_LICENSE>
* NFX (.NET Framework Extension) Unistack Library
* Copyright 2003-2018 Agnicore Inc. portions ITAdapter Corp. Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
</FILE_LICENSE>*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using NFX.IO.FileSystem;
using NFX.IO.FileSystem.SVN;
using NFX.Scripting;
using NFX.Security;
using FS = NFX.IO.FileSystem.FileSystem;

namespace NFX.ITest.IO.FileSystem.SVN
{
  [Runnable]
  public class SVNFileSystemTest: ExternalCfg, IRunnableHook
  {
    private static SVNFileSystemSessionConnectParams CONN_PARAMS, CONN_PARAMS_TIMEOUT;

    void IRunnableHook.Prologue(Runner runner, FID id)
    {
      CONN_PARAMS = FileSystemSessionConnectParams.Make<SVNFileSystemSessionConnectParams>(
        "svn{{ name='aaa' server-url='{0}' user-name='{1}' user-password='{2}' }}".Args(SVN_ROOT, SVN_UNAME, SVN_UPSW));

      CONN_PARAMS_TIMEOUT = FileSystemSessionConnectParams.Make<SVNFileSystemSessionConnectParams>(
        "svn{{ name='aaa' server-url='{0}' user-name='{1}' user-password='{2}' timeout-ms=1 }}".Args(SVN_ROOT, SVN_UNAME, SVN_UPSW));
    }

    bool IRunnableHook.Epilogue(Runner runner, FID id, Exception error) { return false; }

    [Run]
    public void NavigateRootDir()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        using(var fs = new SVNFileSystem("NFX-SVN"))
        {
          var session = fs.StartSession(CONN_PARAMS);

          var dir = session[string.Empty] as FileSystemDirectory;

          Aver.IsNotNull(dir);
          Aver.AreEqual("/", dir.Path);
        }
      }
    }

    [Run]
    public void Size()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];

        using(var session = fs.StartSession())
        {
          var dir = session["trunk/Source/Testing/NUnit/NFX.ITest/IO/FileSystem/SVN/Esc Folder+"] as FileSystemDirectory;

          var file1 = session["trunk/Source/Testing/NUnit/NFX.ITest/IO/FileSystem/SVN/Esc Folder+/Escape.txt"] as FileSystemFile;
          var file2 = session["trunk/Source/Testing/NUnit/NFX.ITest/IO/FileSystem/SVN/Esc Folder+/NestedFolder/Escape.txt"] as FileSystemFile;

          Aver.AreEqual(19UL, file1.Size);
          Aver.AreEqual(11UL, file2.Size);

          Aver.AreEqual(30UL, dir.Size);
        }
      }
    }

    [Run]
    public void NavigateChildDir()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];

        using(var session = fs.StartSession())
        {
          {
            var dir = session["trunk"] as FileSystemDirectory;

            Aver.IsNotNull(dir);
            Aver.AreEqual("/trunk", dir.Path);
            Aver.AreEqual("/", dir.ParentPath);
          }

          {
            var dir = session["trunk/Source"] as FileSystemDirectory;

            Aver.IsNotNull(dir);
            Aver.AreEqual("/trunk/Source", dir.Path);
            Aver.AreEqual("/trunk", dir.ParentPath);
          }
        }
      }
    }

    [Run]
    public void NavigateChildFile()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];
        using (var session = fs.StartSession())
        {
          var file = session["/trunk/Source/NFX/LICENSE.txt"] as FileSystemFile;

          Aver.IsNotNull(file);
          Aver.AreEqual("/trunk/Source/NFX/LICENSE.txt", file.Path);
        }
      }
    }

    [Run]
    public void GetFileContent()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];
        using (var session = fs.StartSession())
        {
          var file = session["/trunk/Source/NFX/LICENSE.txt"] as FileSystemFile;

          Aver.IsTrue(file.ReadAllText().IsNotNullOrEmpty());
        }
      }
    }

    [Run]
    public void GetVersions()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];
        using (var session = fs.StartSession())
        {
          var currentVersion = session.LatestVersion;

          var preVersions = session.GetVersions(currentVersion, 5);

          Aver.AreEqual(5, preVersions.Count());
          Aver.AreEqual(preVersions.Last().Name.AsInt() + 1, currentVersion.Name.AsInt());
        }
      }
    }

    [Run]
    public void GetVersionedFiles()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        IList<WebDAV.Version> versions = WebDAV.GetVersions(SVN_ROOT, SVN_UNAME, SVN_UPSW).ToList();

        WebDAV.Version v192 = versions.First(v => v.Name == "192");
        WebDAV.Version v110 = versions.First(v => v.Name == "110");

        var fs = FS.Instances["NFX-SVN"];
        using (var session = fs.StartSession())
        {
          session.Version = v192;
          var file192 = session["trunk/Source/NFX.Wave/Templatization/StockContent/Embedded/script/wv.js"] as FileSystemFile;
          string content1530 = file192.ReadAllText();

          session.Version = v110;
          var file110 = session["trunk/Source/NFX.Wave/Templatization/StockContent/Embedded/script/wv.js"] as FileSystemFile;
          string content1531 = file110.ReadAllText();

          Aver.AreNotEqual(content1530, content1531);
        }
      }
    }

    [Run]
    [Aver.Throws(typeof(System.Net.WebException), Message = "timed out", MsgMatch = Aver.ThrowsAttribute.MatchType.Contains)]
    public void FailedFastTimeout()
    {
      using(new NFX.ApplicationModel.ServiceBaseApplication(null, LACONF.AsLaconicConfig()))
      {
        var fs = FS.Instances["NFX-SVN"];
        using (var session = fs.StartSession(CONN_PARAMS_TIMEOUT))
        {
          var dir = session[string.Empty] as FileSystemDirectory;
        }
      }
    }
  }
}