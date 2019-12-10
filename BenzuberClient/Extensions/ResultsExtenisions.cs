using System;
using System.Collections.Generic;
using System.Text;
using Benzuber.Api.Models;
using ProjectSummer.Repository;

namespace Benzuber.Extenisions
{
    public static class ResultsExtensions
    {
        public static void ThrowWhenNotOk(this Results result, string messagePrefix)
        {
            if(result != Results.OK)
                throw new Exception($"{messagePrefix}, Result: {result}");
        }
    }
}
