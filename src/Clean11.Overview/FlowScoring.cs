// Adapted from Flow-Launcher/Flow.Launcher, StringMatcher.CalculateSearchScore.
// Source commit: 3b89277c65981c8228f2c0525270a5a716506cca
// Copyright (c) 2019 Flow-Launcher; Copyright (c) 2015 Wox.
// MIT license: see FLOW-LAUNCHER-LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;

static class FlowScoring {
    internal static int Calculate(string query,string candidate,int firstIndex,List<int> spaces,int span,bool contiguous) {
        int score=100*(query.Length+1)/((1+firstIndex)+(span+1));
        if(firstIndex==0&&contiguous)score-=spaces.Count;
        if(candidate.Length-query.Length<5)score+=20;
        else if(candidate.Length-query.Length<10)score+=10;
        if(contiguous) {
            int count=query.Count(c=>!Char.IsWhiteSpace(c));
            score+=count<=4?count*10:40+(count-4)*5;
        }
        return score;
    }
}

sealed class SearchQuery {
    internal string Text,Scope="";
    internal bool Includes(string category) {return Scope.Length==0||Scope==category;}
    internal bool NeedsFiles {get{string answer;if(Scope.Length==0&&QuickCalculator.TryCalculate(Text,out answer))return false;return (Includes("files")||Includes("folders"))&&(Text.Length>=3||System.IO.Path.IsPathRooted(FolderSearch.Normalize(Text)));}}
    internal static SearchQuery Parse(string input) {
        var query=new SearchQuery{Text=(input??"").Trim()};
        int colon=query.Text.IndexOf(':');
        if(colon>0) {
            string prefix=query.Text.Substring(0,colon).Trim().ToLowerInvariant();
            var aliases=new Dictionary<string,string>{{"app","apps"},{"apps","apps"},{"file","files"},{"files","files"},{"folder","folders"},{"folders","folders"},{"window","windows"},{"windows","windows"},{"setting","settings"},{"settings","settings"}};
            string scope;
            if(aliases.TryGetValue(prefix,out scope)){query.Scope=scope;query.Text=query.Text.Substring(colon+1).Trim();}
        }
        return query;
    }
}

// A small arithmetic parser, never a shell/eval invocation. Bounded input and
// recursion make incomplete expressions safe while the user is still typing.
sealed class QuickCalculator {
    string expression;int at,depth;
    void Space(){while(at<expression.Length&&Char.IsWhiteSpace(expression[at]))at++;}
    bool Eat(char c){Space();if(at<expression.Length&&expression[at]==c){at++;return true;}return false;}
    double Sum(){double value=Product();while(true){if(Eat('+'))value+=Product();else if(Eat('-'))value-=Product();else return value;}}
    double Product(){double value=Unary();while(true){if(Eat('*'))value*=Unary();else if(Eat('/'))value/=Unary();else if(Eat('%'))value%=Unary();else return value;}}
    double Unary(){if(Eat('+'))return Unary();if(Eat('-'))return -Unary();return Power();}
    double Power(){double value=Atom();if(Eat('^'))value=Math.Pow(value,Unary());return value;}
    double Atom(){
        if(++depth>24)throw new FormatException();
        try {
            if(Eat('(')){double value=Sum();if(!Eat(')'))throw new FormatException();return value;}
            Space();int start=at;while(at<expression.Length&&(Char.IsDigit(expression[at])||expression[at]=='.'))at++;
            return Double.Parse(expression.Substring(start,at-start),System.Globalization.CultureInfo.InvariantCulture);
        }finally{depth--;}
    }
    internal static bool TryCalculate(string input,out string answer) {
        answer=null;if(String.IsNullOrWhiteSpace(input)||input.Length>160)return false;
        string text=input.Trim().TrimStart('=');
        if(!text.Any(c=>"+-*/%^".Contains(c))||text.Any(c=>!Char.IsDigit(c)&&!Char.IsWhiteSpace(c)&&!".+-*/%^()".Contains(c)))return false;
        try {var parser=new QuickCalculator{expression=text};double value=parser.Sum();parser.Space();if(parser.at!=text.Length||Double.IsNaN(value)||Double.IsInfinity(value))return false;answer=value.ToString("G12",System.Globalization.CultureInfo.InvariantCulture);return true;}catch{return false;}
    }
}
