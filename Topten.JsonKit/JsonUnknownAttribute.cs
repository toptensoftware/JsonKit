﻿// JsonKit v0.5 - A simple but flexible Json library in a single .cs file.
// 
// Copyright (C) 2014 Topten Software (contact@toptensoftware.com) All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this product 
// except in compliance with the License. You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software distributed under the 
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, 
// either express or implied. See the License for the specific language governing permissions 
// and limitations under the License.

using System;


namespace Topten.JsonKit
{
    // Apply to enum values to specify which enum value to select
    // if the supplied json value doesn't match any.
    // If not found throws an exception
    // eg, any unknown values in the json will be mapped to Fruit.unknown
    //
    //	 [JsonUnknown(Fruit.unknown)]
    //   enum Fruit
    //   {
    // 		unknown,
    //      Apple,
    //      Pear,
    //	 }
    [AttributeUsage(AttributeTargets.Enum)]
	public class JsonUnknownAttribute : Attribute
	{
		public JsonUnknownAttribute(object unknownValue)
		{
			UnknownValue = unknownValue;
		}

		public object UnknownValue
		{
			get;
			private set;
		}
	}
}
