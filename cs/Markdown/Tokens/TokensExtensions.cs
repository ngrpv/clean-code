﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Markdown.Tags;

namespace Markdown.Tokens
{
    public static class TokensExtensions
    {
        public static IEnumerable<Token> RemoveEscaping(this IList<Token> tokens)
        {
            var escapeNext = false;
            for (var i = 0; i < tokens.Count; i++)
            {
                if (escapeNext)
                {
                    escapeNext = false;
                    tokens[i].SwitchToText();
                    yield return tokens[i];
                    continue;
                }

                if (tokens[i].TokenType == TokenType.Escape)
                {
                    if (tokens[i + 1].TokenType != TokenType.Text)
                    {
                        escapeNext = true;
                        continue;
                    }

                    tokens[i].SwitchToText();
                }

                yield return tokens[i];
            }
        }

        public static IEnumerable<Token> RemoveUnpaired(this IEnumerable<Token> tokens, string text)
        {
            var tagsStack = new Stack<Token>();
            var undefinedTags = new Stack<Token>();
            foreach (var token in tokens)
            {
                if (token.TokenType == TokenType.Text)
                {
                    var textValue = text.Substring(token.Start, token.Length);
                    if (textValue.Contains(" "))
                    {
                        foreach (var token1 in ReturnAsText(undefinedTags)) yield return token1;
                    }

                    yield return token;
                    continue;
                }

                switch (token.TagRole)
                {
                    case TagRole.Opening:
                        tagsStack.Push(token);
                        break;
                    case TagRole.Closing:
                    {
                        foreach (var returnedToken in HandleClosingTag(tagsStack, undefinedTags, token))
                            yield return returnedToken;
                        break;
                    }
                    case TagRole.Undefined:
                        foreach (var returnedFromUndefinedHandler in HandleUndefinedTag(text, undefinedTags, tagsStack,
                            token))
                            yield return returnedFromUndefinedHandler;
                        break;
                    default:
                        yield return token;
                        break;
                }
            }

            foreach (var token in ReturnAsText(undefinedTags)) yield return token;
            foreach (var token in ReturnAsText(tagsStack)) yield return token;
        }

        public static void AddTextBetween(this IList<Token> tokens, TagToken firstTag, TagToken secondTag)
        {
            var textStart = firstTag.Start + firstTag.Length;
            var textLength = secondTag.Start - textStart;
            if (textLength > 0)
                tokens.Add(new Token(TokenType.Text, textStart, textLength));
        }

        public static void AddTextBefore(this IList<Token> tokens, TagToken token)
        {
            tokens.AddTextBetween(new TagToken(0, 0), token);
        }

        public static void AddTextAfter(this IList<Token> tokens, TagToken token, int end)
        {
            tokens.AddTextBetween(token, new TagToken(end + 1, 0));
        }

        private static IEnumerable<Token> HandleUndefinedTag(string text, Stack<Token> undefinedTags,
            Stack<Token> tagsStack, Token token)
        {
            if (undefinedTags.Count == 0)
            {
                if (tagsStack.Count > 0 && tagsStack.Peek().TagType == token.TagType)
                {
                    var textBetween = text.Substring(tagsStack.Peek().End + 1,
                        token.Start - tagsStack.Peek().End - 1);
                    if (!textBetween.Contains(' '))
                    {
                        token.TagRole = TagRole.Closing;
                        yield return tagsStack.Pop();
                        yield return token;
                    }
                    else
                        undefinedTags.Push(token);
                }
                else
                    undefinedTags.Push(token);
            }
            else if (undefinedTags.Peek().TagType == token.TagType)
            {
                undefinedTags.Peek().TagRole = TagRole.Opening;
                token.TagRole = TagRole.Closing;
                yield return undefinedTags.Pop();
                yield return token;
            }
            else
                undefinedTags.Push(token);
        }

        private static IEnumerable<Token> HandleClosingTag(Stack<Token> tagsStack, Stack<Token> undefinedTags,
            Token token)
        {
            if (tagsStack.Count <= 0)
            {
                if (undefinedTags.Count > 0 && undefinedTags.Peek().TagType == token.TagType)
                {
                    undefinedTags.Peek().TagRole = TagRole.Opening;
                    yield return undefinedTags.Pop();
                    yield return token;
                    yield break;
                }

                token.SwitchToText();
                yield return token;
                yield break;
            }

            var previous = tagsStack.Pop();
            if (previous.TagType != token.TagType)
            {
                previous.SwitchToText();
                token.SwitchToText();
            }

            yield return previous;
            yield return token;
        }


        private static IEnumerable<Token> ReturnAsText(Stack<Token> tokens)
        {
            foreach (var token in tokens)
            {
                token.SwitchToText();
                yield return token;
            }

            tokens.Clear();
        }
    }
}