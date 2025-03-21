using System;
using System.Linq;
using System.Text;

namespace ConfuserEx_Additions.Jongyeol;

public class Rename {

    private static readonly char[] UnicodeCharset = new char[] { }
        .Concat(Enumerable.Range(0x200b, 5).Select(ord => (char)ord))
        .Concat(Enumerable.Range(0x2029, 6).Select(ord => (char)ord))
        .Concat(Enumerable.Range(0x206a, 6).Select(ord => (char)ord))
        .Except(['\u2029'])
        .ToArray();

    public static string RandomName() {
        if(true) return SimpleName();
        byte[] bytes = new byte[40];
        Utils.Random.NextBytes(bytes);
        return EncodeString(bytes, UnicodeCharset);
    }

    public static string SimpleName() {
        char[] chars = new char[6];
        for(int i = 0; i < 6; i++) chars[i] = (char) Utils.Random.Next(97, 122);
        return new string(chars);
    }

    public static string ASDFName() {
        char[] chars = new char[80];
        chars[0] = 'i';
        chars[1] = ' ';
        for(int i = 2; i < 80; i++) chars[i] = UnicodeCharset[Utils.Random.Next(UnicodeCharset.Length)];
        return new string(chars);
    }

    public static string EncodeString(byte[] buff, char[] charset) {
        int current = buff[0];
        StringBuilder ret = new();
        ret.Append(charset[Utils.Random.Next(charset.Length)]);
        switch(Utils.Random.Next(4)) {
            case 0:
                ret.Append("Subcribe To Jongyeol");
                break;
            case 1:
                ret.Append("Are You Try Decompile?");
                break;
            case 2:
                ret.Append("This Code Is Obfuscated");
                break;
            case 3:
                ret.Append("Dont Try Decompile!");
                break;
        }
        for(int i = 1; i < buff.Length; i++) {
            current = (current << 8) + buff[i];
            while(current >= charset.Length) {
                current = Math.DivRem(current, charset.Length, out int remainder);
                ret.Append(charset[remainder]);
            }
        }
        if(current != 0) ret.Append(charset[current % charset.Length]);
        ret.Append('\u206e');
        return ret.ToString();
    }
}
