using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SkPluginComponents.Models
{
    public enum InputType
    {
        Simple, Complex
    }
    public enum SimpleInputType
    {
        Text, 
        Number,
        Boolean, 
        TextArea, 
        Date,
        Select
    }
}
