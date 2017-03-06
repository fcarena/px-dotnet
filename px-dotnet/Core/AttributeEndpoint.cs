﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MercadoPago
{

    public class GETEndpoint : Attribute
    {
        private string Path;

        public GETEndpoint(string path)
        {
            this.Path = path;
        }

    }

    public class POSTEndpoint : Attribute  
    {
        private string Path;

        public POSTEndpoint(string path)
        {
            this.Path = path;
        }

    }

    public class PUTEndpoint : Attribute
    {
        private string Path;

        public PUTEndpoint(string path)
        {
            this.Path = path;
        }

    }

    public class DELETEEndpoint : Attribute
    {
        private string Path;

        public DELETEEndpoint(string path)
        {
            this.Path = path;
        }

    }
}
