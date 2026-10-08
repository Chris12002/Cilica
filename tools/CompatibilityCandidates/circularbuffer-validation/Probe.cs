using System;
using CircularBuffer;
namespace Cilica.CandidateValidation
{
    // Authored transcript adapter; the pinned candidate algorithm remains unchanged.
    public static class CircularBufferProbe
    {
        private static void Add(int[] values,ref int count,int value){values[count++]=value;}
        private static void Text(int[] values,ref int count,string value)
        {
            if(value==null){Add(values,ref count,-1);return;}
            Add(values,ref count,value.Length);for(int i=0;i<value.Length;++i)Add(values,ref count,value[i]);
        }
        private static void Fault(int[] values,ref int count,int kind,string message,string parameter)
        {Add(values,ref count,kind);Text(values,ref count,message);Text(values,ref count,parameter);}
        private static void State(CircularBuffer<int> buffer,int[] values,ref int count)
        {
            Add(values,ref count,buffer.Capacity);Add(values,ref count,buffer.Size);
            Add(values,ref count,buffer.IsFull?1:0);Add(values,ref count,buffer.IsEmpty?1:0);
            for(int i=0;i<buffer.Size;++i)Add(values,ref count,buffer[i]);
            if(buffer.Size!=0){Add(values,ref count,buffer.Front());Add(values,ref count,buffer.Back());}
        }
        private static void Step(CircularBuffer<int> buffer,int operation,int value,int[] values,ref int count)
        {
            Add(values,ref count,operation);Add(values,ref count,value);
            try
            {
                if(operation==0)buffer.PushBack(value);
                else if(operation==1)buffer.PushFront(value);
                else if(operation==2)buffer.PopBack();
                else if(operation==3)buffer.PopFront();
                else if(operation==4)Add(values,ref count,buffer[value]);
                else if(operation==5)buffer[value]=unchecked(value*17+31);
                else if(operation==6)buffer.Clear();
                else if(operation==7)Add(values,ref count,buffer.Front());
                else if(operation==8)Add(values,ref count,buffer.Back());
                Fault(values,ref count,0,null,null);
            }
            catch(ArgumentNullException e){Fault(values,ref count,1,e.Message,e.ParamName);}
            catch(ArgumentException e){Fault(values,ref count,2,e.Message,e.ParamName);}
            catch(IndexOutOfRangeException e){Fault(values,ref count,3,e.Message,null);}
            catch(InvalidOperationException e){Fault(values,ref count,4,e.Message,null);}
            State(buffer,values,ref count);
        }
        public static int Run(int scenario,int observation)
        {
            if(scenario<0||scenario>=48)throw new ArgumentOutOfRangeException("scenario");
            var values=new int[4096];int count=0;Add(values,ref count,scenario);
            if(scenario<8)
            {
                int capacity=scenario+1;int[] seed=new int[scenario%3];for(int i=0;i<seed.Length;++i)seed[i]=unchecked(int.MinValue+17*i);
                var buffer=new CircularBuffer<int>(capacity,seed);if(seed.Length!=0)seed[0]=101;
                State(buffer,values,ref count);
            }
            else if(scenario<16)
            {
                var buffer=new CircularBuffer<int>(1+(scenario-8)%4);State(buffer,values,ref count);
                for(int i=0;i<14;++i)Step(buffer,(i+scenario)%4,unchecked(int.MinValue+i*103+scenario),values,ref count);
            }
            else if(scenario<24)
            {
                int capacity=(scenario-16)%4-1;int[] seed=scenario<20?null:new int[]{11,13,17};
                try{var buffer=new CircularBuffer<int>(capacity,seed);Fault(values,ref count,0,null,null);State(buffer,values,ref count);}
                catch(ArgumentNullException e){Fault(values,ref count,1,e.Message,e.ParamName);}
                catch(ArgumentException e){Fault(values,ref count,2,e.Message,e.ParamName);}
            }
            else if(scenario<32)
            {
                var buffer=new CircularBuffer<int>(4,new int[]{11,13,17});buffer.PushFront(19);
                int index=scenario==24?int.MinValue:scenario==25?-2:scenario==26?-1:scenario==27?0:scenario==28?2:scenario==29?3:scenario==30?4:int.MaxValue;
                Step(buffer,4,index,values,ref count);Step(buffer,5,index,values,ref count);
            }
            else if(scenario<40)
            {
                int capacity=scenario-31;var buffer=new CircularBuffer<int>(capacity);
                for(int i=0;i<capacity*2+1;++i)buffer.PushBack(unchecked(i*31+7));
                State(buffer,values,ref count);Step(buffer,6,0,values,ref count);
                Step(buffer,0,int.MinValue,values,ref count);Step(buffer,1,int.MaxValue,values,ref count);
                Step(buffer,6,0,values,ref count);Step(buffer,6,0,values,ref count);
            }
            else
            {
                var buffer=new CircularBuffer<int>(2);int choice=(scenario-40)%4;
                Step(buffer,choice==0?7:choice==1?8:choice==2?2:3,0,values,ref count);
                Step(buffer,0,scenario,values,ref count);Step(buffer,2,0,values,ref count);Step(buffer,3,0,values,ref count);
            }
            return observation<0?count:values[observation];
        }
    }
}
