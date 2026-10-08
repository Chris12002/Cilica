using System;
namespace Cilica.CandidateValidation
{
    // Owned transcript adapter. Execute only with the reviewed pinned upstream source
    // after explicit approval, inside the documented offline isolated environment.
    public static class AlbireoDecodeProbe
    {
        public static string Source(int scenario)
        {
            switch(scenario)
            {
                case 0:return null;case 1:return "";
                case 2:return "MY======";case 3:return "MZXQ====";case 4:return "MZXW6===";
                case 5:return "MZXW6YQ=";case 6:return "MZXW6YTB";case 7:return "MZXW6YTBOI======";
                case 8:return "mzxw6ytboi======";case 9:return "M";case 10:return "======";
                case 11:return "MY";case 12:return "A===";case 13:return "AAAA";
                case 14:return "0";case 15:return "A A";case 16:return "A\0A";
                case 17:return "\u017fA";case 18:return "\u0131A";case 19:return "\u0130A";
                case 20:return "\ud800";case 21:return "\udc00";case 22:return "\ud801\udc28";
                case 23:return "MY====a";case 24:return "MY====== ";case 25:return "=\0=";
                case 26:return "77777777";case 27:return "AAAAAAAA";
                case 28:case 29:case 31:
                    string alphabet=scenario==29?"abcdefghijklmnopqrstuvwxyz234567":"ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
                    var chars=new char[scenario==31?256:257];
                    for(int i=0;i<chars.Length;i++)chars[i]=alphabet[i%32];
                    return new string(chars);
                case 30:return "\u00dfA";
                default:throw new ArgumentOutOfRangeException("scenario");
            }
        }
        // Transcript: status, length, reference identity of two results, message
        // length, parameter length, source length, every output byte, message and
        // parameter UTF-16 units. No digest replaces a byte/string observation.
        public static int Run(int scenario,int observation)
        {
            string source=Source(scenario),message="",parameter="";
            byte[] first=null,second=null;int status=0;
            try { first=Albireo.Base32.Base32.Decode(in source);second=Albireo.Base32.Base32.Decode(in source); }
            catch(ArgumentNullException ex){status=1;message=ex.Message;parameter=ex.ParamName??"";}
            catch(ArgumentException ex){status=2;message=ex.Message;parameter=ex.ParamName??"";}
            catch(OverflowException ex){status=3;message=ex.Message;}
            catch(IndexOutOfRangeException ex){status=4;message=ex.Message;}
            int length=first==null?0:first.Length;
            if(observation==-1)return 6+length+message.Length+parameter.Length;
            if(observation==0)return status;
            if(observation==1)return first==null?-1:first.Length;
            if(observation==2)return first==null?-1:ReferenceEquals(first,second)?1:0;
            if(observation==3)return message.Length;
            if(observation==4)return parameter.Length;
            if(observation==5)return source==null?-1:source.Length;
            observation-=6;
            if(observation<length)return first[observation];observation-=length;
            if(observation<message.Length)return message[observation];observation-=message.Length;
            if(observation<parameter.Length)return parameter[observation];
            throw new ArgumentOutOfRangeException("observation");
        }
    }
}
