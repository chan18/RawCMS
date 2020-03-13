﻿//******************************************************************************
// <copyright file="license.md" company="RawCMS project  (https://github.com/arduosoft/RawCMS)">
// Copyright (c) 2019 RawCMS project  (https://github.com/arduosoft/RawCMS)
// RawCMS project is released under GPL3 terms, see LICENSE file on repository root at  https://github.com/arduosoft/RawCMS .
// </copyright>
// <author>Daniele Fontani, Emanuele Bucarelli, Francesco Mina'</author>
// <autogenerated>true</autogenerated>
//******************************************************************************
namespace RawCMS.Library.Schema.Validation
{
    public class IntValidation : BaseJavascriptValidator
    {
        public override string Type => "int";

        public override string Javascript
        {
            get
            {
                return @"
const innerValidation = function() {
    if (value === null || value === undefined) {
        return;
    }

    // code starts here
    intVal = parseInt(value);

    if (isNaN(intVal) || intVal  === NaN ) {
        errors.push({""Code"":""INT - 01"", ""Title"":""Not a number""});
        return;
    }

    if (!(parseFloat(value) === intVal)) {
        var err=""Value ""+value+""not an INT number"";
        errors.push({""Code"":""INT - 04"", ""Title"":err});
        return;
    }

    if (options.min !== undefined && options.min > intVal) {
        errors.push({""Code"":""INT-02"", ""Title"":""less than minimum"",""Description"":""ddd""});
    }

    if (options.max !== undefined && options.max < intVal)
    {
        errors.push({""Code"":""INT-03"", ""Title"":""greater than max"",""Description"":""ddd""});
    }

    return JSON.stringify(errors);
};

var backendResult = innerValidation();
            ";
            }
        }
    }
}