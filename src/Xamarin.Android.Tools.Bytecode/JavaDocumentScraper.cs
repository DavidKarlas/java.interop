﻿/* 
 *  Copyright (c) 2011 Xamarin Inc.
 * 
 *  Permission is hereby granted, free of charge, to any person 
 *  obtaining a copy of this software and associated documentation 
 *  files (the "Software"), to deal in the Software without restriction, 
 *  including without limitation the rights to use, copy, modify, merge, 
 *  publish, distribute, sublicense, and/or sell copies of the Software, 
 *  and to permit persons to whom the Software is furnished to do so, 
 *  subject to the following conditions:
 * 
 *  The above copyright notice and this permission notice shall be 
 *  included in all copies or substantial portions of the Software.
 * 
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
 *  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
 *  OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
 *  NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS 
 *  BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN 
 *  ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN 
 *  CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 *  SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text.RegularExpressions;

namespace Xamarin.Android.Tools.Bytecode
{
	enum JavaDocKind {
		DroidDoc,
		Java6,
		Java7
	}
	
	class DroidDocScraper : AndroidDocScraper
	{
		const String pattern_head_droiddoc = "<span class=\"sympad\"><a href=\".*";
	
		public DroidDocScraper (string dir)
			: base (dir, pattern_head_droiddoc, null, " ", false)
		{
		}
	}
	
	class JavaDocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<TD><CODE><B><A HREF=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<TD><CODE>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";
	
		public JavaDocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, false)
		{
		}
	}
	
	class Java7DocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<td class=\"col.+\"><code><strong><a href=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<td><code>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";
	
		public Java7DocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, true)
		{
		}
	}

	class Java8DocScraper : AndroidDocScraper
	{
		const String pattern_head_javadoc = "<td class=\"col.+\"><code><strong><a href=\"[./]*"; // I'm not sure how path could be specified... (./ , ../ , or even /)
		const String reset_pattern_head_javadoc = "<td><code>";
		const String parameter_pair_splitter_javadoc = "&nbsp;";

		public Java8DocScraper (string dir)
			: base (dir, pattern_head_javadoc, reset_pattern_head_javadoc, parameter_pair_splitter_javadoc, true, "-", "-", "-")
		{
		}
	}

	public abstract class AndroidDocScraper
	{
		readonly String pattern_head;
		readonly String reset_pattern_head;
		readonly char [] parameter_pair_splitter;
		readonly bool continuous_param_lines;
		readonly String open_method;
		readonly String param_sep;
		readonly String close_method;	
		string root;
	
		protected AndroidDocScraper (string dir, String patternHead, String resetPatternHead, String parameterPairSplitter, bool continuousParamLines)
			: this (dir, patternHead, resetPatternHead, parameterPairSplitter, continuousParamLines, "\\(", ", ", "\\)")
		{
		}

		protected AndroidDocScraper (string dir, String patternHead, String resetPatternHead, String parameterPairSplitter, bool continuousParamLines, string openMethod, string paramSep, string closeMethod)
		{
			if (dir == null)
				throw new ArgumentNullException ("dir");
	
			pattern_head = patternHead;
			reset_pattern_head = resetPatternHead;
			parameter_pair_splitter = (parameterPairSplitter != null ? parameterPairSplitter : "\\s+").ToCharArray ();
			continuous_param_lines = continuousParamLines;
			open_method = openMethod;
			param_sep = paramSep;
			close_method = closeMethod;	
			if (!Directory.Exists (dir))
				throw new Exception ("Directory '" + dir + "' does not exist");
	
			root = dir;
	
			if (!File.Exists (Path.Combine (dir, "package-list")) && !File.Exists (Path.Combine (dir, "packages.html")))
				throw new ArgumentException ("Directory '" + dir + "' does not appear to be an android doc reference directory.");
			
			//foreach (var f in Directory.GetFiles (dir, "*.html", SearchOption.AllDirectories))
			//	LoadDocument (f.Substring (dir.Length + 1), f);
		}
		
		void LoadDocument (string packageDir, string file)
		{
			string pkgName = packageDir.Replace ('/', '.');
			string className = Path.GetFileNameWithoutExtension (file).Replace ('$', '.');
			
			string html = File.ReadAllText (file);
		}
	
		public String[] GetParameterNames (string package, string type, string method, string[] ptypes, bool isVarArgs)
		{
			String path = package.Replace ('.', '/') + '/' + type.Replace ('$', '.') + ".html";
			string file = Path.Combine (root, path);
			if (!File.Exists (file)) {
				Log.Warning (1,"Warning: no document found : " + file);
				return null;
			}
	
			var buffer = new StringBuilder ();
			buffer.Append (pattern_head);
			buffer.Append (path);
			buffer.Append ("#");
			buffer.Append (method);
			buffer.Append (open_method);
			for (int i = 0; i < ptypes.Length; i++) {
				if (i != 0)
					buffer.Append (param_sep);
				buffer.Append (ptypes [i].Replace ('$', '.'));
			}
			buffer.Append (close_method);
			buffer.Append ("\".*\\((.*)\\)");
			buffer.Replace ("[", "\\[").Replace ("]", "\\]").Replace ("?", "\\?");
			Regex pattern = new Regex (buffer.ToString (), RegexOptions.Multiline);
	
			try {
				var reader = File.OpenText (file);
				try {
					String text = "";
					String prev = null;
					while ((text = reader.ReadLine ()) != null) {
						if (prev != null)
							prev = text = prev + text;
						var matcher = pattern.Match (text);
						if (matcher.Success) {
							var plist = matcher.Groups [1];
							String[] parms = plist.Value.Split (new string [] {", "}, StringSplitOptions.RemoveEmptyEntries);
							if (parms.Length != ptypes.Length) {
								Log.Warning (1, "failed matching {0} (expected {1} params, got {2} params)", buffer, ptypes.Length, parms.Length);
								return null;
							}
							String[] result = new String [ptypes.Length];
							for (int i = 0; i < ptypes.Length; i++) {
								String[] toks = parms [i].Split (parameter_pair_splitter);
								result [i] = toks [toks.Length - 1];
							}
							reader.Close ();
							return result;
						}
						// sometimes we get incomplete tag, so cache it until it gets complete or matched.
						// I *know* this is a hack.
						if (reset_pattern_head == null || text.EndsWith (">", StringComparison.Ordinal) || !continuous_param_lines && !text.StartsWith (reset_pattern_head, StringComparison.Ordinal))
							prev = null;
						else
							prev = text;
					}
				} finally {
					reader.Close ();
				}
			} catch (Exception e) {
				Log.Error ("ERROR in {0}.{1}: {2}", type, method, e);
				return null;
			}
	
			Log.Warning (1, "Warning : no match for {0}.{1} (rex: {2})", type, method, buffer);
			return null;
		}
		
		static Dictionary<String,List<String>> deprecatedFields;
		static Dictionary<String,List<String>> deprecatedMethods;
		
		public static void LoadXml (String filename)
		{
			try {
				var doc = XDocument.Load (filename);
				deprecatedFields = new Dictionary<String,List<String>> ();
				deprecatedMethods = new Dictionary<String,List<String>> ();
				var files = doc.Root.Descendants ("file");
				foreach (var file in files) {
					var f = new List<String> ();
					deprecatedFields [file.Attribute ("name").Value] = f;
					var fields = file.Descendants ("field");
					foreach (var fld in fields)
						f.Add (fld.Value);
	
					var m = new List<String> ();
					deprecatedMethods [file.Attribute ("name").Value] = m;
					var methods = file.Descendants ("method");
					foreach (var meh in methods)
						m.Add (meh.Value);
				}
			
			} catch (Exception ex) {
				Log.Error ("Annotations parser error: " + ex);
			}
		}		
	}
}
