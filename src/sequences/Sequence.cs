﻿//
//  WinCompose — a compose key for Windows — http://wincompose.info/
//
//  Copyright © 2013—2015 Sam Hocevar <sam@hocevar.net>
//              2014—2015 Benjamin Litzelmann
//
//  This program is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace WinCompose
{

public class KeyConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
    {
        var strValue = value as string;
        if (strValue != null)
        {
            if (strValue.StartsWith("VK."))
            {
                try
                {
                    var enumValue = Enum.Parse(typeof(VK), strValue.Substring(3));
                    return new Key((VK)enumValue);
                }
                catch
                {
                    // Silently catch parsing exception.
                }
            }
            return new Key(strValue);
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
    {
        return destinationType == typeof(string) ? value.ToString() : base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>
/// The Key class describes anything that can be done on the keyboard,
/// so either a printable string or a virtual key code.
/// </summary>
[TypeConverter(typeof(KeyConverter))]
public class Key
{
    private static readonly Dictionary<VK, string> m_key_labels = new Dictionary<VK, string>
    {
        { VK.UP,    "▲" },
        { VK.DOWN,  "▼" },
        { VK.LEFT,  "◀" },
        { VK.RIGHT, "▶" },
    };

    private static readonly Dictionary<VK, string> m_key_names = new Dictionary<VK, string>
    {
        { VK.LMENU, i18n.Text.KeyLMenu },
        { VK.RMENU, i18n.Text.KeyRMenu },
        { VK.LCONTROL, i18n.Text.KeyLControl },
        { VK.RCONTROL, i18n.Text.KeyRControl },
        { VK.LWIN, i18n.Text.KeyLWin },
        { VK.RWIN, i18n.Text.KeyRWin },
        { VK.CAPITAL, i18n.Text.KeyCapital },
        { VK.NUMLOCK, i18n.Text.KeyNumLock },
        { VK.PAUSE, i18n.Text.KeyPause },
        { VK.APPS, i18n.Text.KeyApps },
        { VK.ESCAPE, i18n.Text.KeyEscape },
        { VK.SCROLL, i18n.Text.KeyScroll },
        { VK.INSERT, i18n.Text.KeyInsert },
    };

    private readonly VK m_vk;

    private readonly string m_str;

    public Key(string str) { m_str = str; }

    public Key(VK vk) { m_vk = vk; }

    public VK VirtualKey { get { return m_vk; } }

    public bool IsPrintable()
    {
        return m_str != null;
    }

    /// <summary>
    /// A friendly name that we can put in e.g. a dropdown menu
    /// </summary>
    public string FriendlyName
    {
        get
        {
            string ret;
            if (m_key_names.TryGetValue(m_vk, out ret))
                return ret;

            return m_str ?? string.Format("VK.{0}", m_vk);
        }
    }

    /// <summary>
    /// A label that we can print on keycap icons
    /// </summary>
    public string KeyLabel
    {
        get
        {
            string ret;
            if (m_key_labels.TryGetValue(m_vk, out ret))
                return ret;

            return m_str ?? string.Format("VK.{0}", m_vk);
        }
    }

    /// <summary>
    /// Serialize key to a printable string we can parse back into
    /// a <see cref="Key"/> object
    /// </summary>
    public override string ToString()
    {
        return m_str ?? string.Format("VK.{0}", m_vk);
    }

    public override bool Equals(object o)
    {
        return o is Key && this == (o as Key);
    }

    public static bool operator ==(Key a, Key b)
    {
        bool is_a_null = ReferenceEquals(a, null);
        bool is_b_null = ReferenceEquals(b, null);
        if (is_a_null || is_b_null)
            return is_a_null == is_b_null;
        return a.m_str != null ? a.m_str == b.m_str : a.m_vk == b.m_vk;
    }

    public static bool operator !=(Key a, Key b)
    {
        return !(a == b);
    }

    public override int GetHashCode()
    {
        return m_str != null ? m_str.GetHashCode() : ((int)m_vk).GetHashCode();
    }
};

/// <summary>
/// The KeySequence class describes a sequence of keys, which can be
/// compared with other lists of keys.
/// </summary>
public class KeySequence : List<Key>
{
    public KeySequence() : base(new List<Key>()) {}

    public KeySequence(List<Key> val) : base(val) {}

    public override bool Equals(object o)
    {
        if (!(o is KeySequence))
            return false;

        if (Count != (o as KeySequence).Count)
            return false;

        for (int i = 0; i < Count; ++i)
            if (this[i] != (o as KeySequence)[i])
                return false;

        return true;
    }

    public override int GetHashCode()
    {
        int hash = 0x2d2816fe;
        for (int i = 0; i < Count; ++i)
            hash = hash * 31 + this[i].GetHashCode();
        return hash;
    }
};

/*
 * This data structure is used for communication with the GUI
 */

public class SequenceDescription : IComparable<SequenceDescription>
{
    public List<Key> Sequence = new List<Key>();
    public string Description = "";
    public string Result = "";
    public int Utf32 = -1;

    public int CompareTo(SequenceDescription other)
    {
        // If any sequence leads to a single character, compare actual
        // Unicode codepoints rather than strings
        if (Utf32 != -1 || other.Utf32 != -1)
            return Utf32.CompareTo(other.Utf32);
        return Result.CompareTo(other.Result);
    }
};

/*
 * The SequenceTree class contains a tree of all valid sequences, where
 * each child is indexed by the sequence key.
 */

public class SequenceTree
{
    public void Add(List<Key> sequence, string result, int utf32, string desc)
    {
        if (sequence.Count == 0)
        {
            m_result = result;
            m_utf32 = utf32;
            m_description = desc;
            return;
        }

        if (!m_children.ContainsKey(sequence[0]))
            m_children.Add(sequence[0], new SequenceTree());

        var subsequence = sequence.GetRange(1, sequence.Count - 1);
        m_children[sequence[0]].Add(subsequence, result, utf32, desc);
    }

    public bool IsValidPrefix(List<Key> sequence)
    {
        SequenceTree subtree = GetSubtree(sequence);
        return subtree != null;
    }

    public bool IsValidSequence(List<Key> sequence)
    {
        SequenceTree subtree = GetSubtree(sequence);
        return subtree != null && subtree.m_result != null;
    }

    public string GetSequenceResult(List<Key> sequence)
    {
        SequenceTree tree = GetSubtree(sequence);
        return tree == null ? "" : tree.m_result == null ? "" : tree.m_result;
    }

    public SequenceTree GetSubtree(List<Key> sequence)
    {
        if (sequence.Count == 0)
            return this;
        if (!m_children.ContainsKey(sequence[0]))
            return null;
        var subsequence = sequence.GetRange(1, sequence.Count - 1);
        return m_children[sequence[0]].GetSubtree(subsequence);
    }

    public List<SequenceDescription> GetSequenceDescriptions()
    {
        List<SequenceDescription> ret = new List<SequenceDescription>();
        BuildSequenceDescriptions(ret, new List<Key>());
        ret.Sort();
        return ret;
    }

    private void BuildSequenceDescriptions(List<SequenceDescription> list,
                                           List<Key> path)
    {
        if (m_result != null)
        {
            var item = new SequenceDescription();
            item.Sequence = path;
            item.Result = m_result;
            item.Description = m_description;
            item.Utf32 = m_utf32;
            list.Add(item);
        }

        foreach (var pair in m_children)
        {
            var newpath = new List<Key>(path);
            newpath.Add(pair.Key);
            pair.Value.BuildSequenceDescriptions(list, newpath);
        }
    }

    private Dictionary<Key, SequenceTree> m_children
        = new Dictionary<Key, SequenceTree>();
    private string m_result;
    private string m_description;
    private int m_utf32;
};

}
