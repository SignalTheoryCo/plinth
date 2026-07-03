/// Minimal markdown preview renderer. No external markdown library —
/// just enough structure to read a note: headings, bullets, paragraphs,
/// plus clickable [[wiki links]] and #tags, **bold** and `code` inline.
module Plinth.Utils.Markdown

open System.Text.RegularExpressions
open Feliz

/// One pass over a line picks out, in order of the alternation:
/// group 1 = [[link]], group 2/3 = (leading ws)#tag, group 4 = bold, group 5 = code.
let private inlineRe =
    Regex(@"\[\[([^\[\]]+)\]\]|(^|\s)#([A-Za-z][A-Za-z0-9_/-]*)|\*\*([^\*]+)\*\*|`([^`]+)`")

let private renderInline (text: string) (onLink: string -> unit) (onTag: string -> unit) =
    let matches = inlineRe.Matches(text) |> Seq.cast<Match> |> List.ofSeq

    if List.isEmpty matches then
        [ Html.text text ]
    else
        let parts = ResizeArray<ReactElement>()
        let mutable last = 0

        for m in matches do
            if m.Index > last then
                parts.Add(Html.text (text.Substring(last, m.Index - last)))

            if m.Groups.[1].Success then
                let target = m.Groups.[1].Value.Trim()

                parts.Add(
                    Html.a [
                        prop.className
                            "cursor-pointer text-emerald-700 underline decoration-emerald-300 hover:text-emerald-900"
                        prop.onClick (fun e ->
                            e.preventDefault ()
                            onLink target)
                        prop.text target
                    ]
                )
            elif m.Groups.[3].Success then
                if m.Groups.[2].Value <> "" then
                    parts.Add(Html.text m.Groups.[2].Value)

                let tag = m.Groups.[3].Value.ToLowerInvariant()

                parts.Add(
                    Html.button [
                        prop.className "cursor-pointer text-amber-700 hover:text-amber-900"
                        prop.onClick (fun _ -> onTag tag)
                        prop.text ("#" + m.Groups.[3].Value)
                    ]
                )
            elif m.Groups.[4].Success then
                parts.Add(Html.strong [ prop.text m.Groups.[4].Value ])
            elif m.Groups.[5].Success then
                parts.Add(
                    Html.code [
                        prop.className "rounded bg-stone-100 px-1 text-sm"
                        prop.text m.Groups.[5].Value
                    ]
                )

            last <- m.Index + m.Length

        if last < text.Length then
            parts.Add(Html.text (text.Substring(last)))

        List.ofSeq parts

/// Render a whole note body as a preview.
let render (content: string) (onLink: string -> unit) (onTag: string -> unit) =
    let blocks =
        content.Replace("\r\n", "\n").Split('\n')
        |> Array.toList
        |> List.mapi (fun i line ->
            let t = line.TrimEnd()

            if t.StartsWith("### ") then
                Html.h3 [
                    prop.key i
                    prop.className "mt-4 mb-1 text-lg font-semibold"
                    prop.children (renderInline (t.Substring 4) onLink onTag)
                ]
            elif t.StartsWith("## ") then
                Html.h2 [
                    prop.key i
                    prop.className "mt-5 mb-2 text-xl font-semibold"
                    prop.children (renderInline (t.Substring 3) onLink onTag)
                ]
            elif t.StartsWith("# ") then
                Html.h1 [
                    prop.key i
                    prop.className "mt-2 mb-3 font-serif text-2xl font-bold"
                    prop.children (renderInline (t.Substring 2) onLink onTag)
                ]
            elif t.StartsWith("- ") then
                Html.div [
                    prop.key i
                    prop.className "ml-2 flex gap-2"
                    prop.children (
                        Html.span [ prop.text "•" ]
                        :: renderInline (t.Substring 2) onLink onTag
                    )
                ]
            elif t = "" then
                Html.div [ prop.key i; prop.className "h-3" ]
            else
                Html.p [
                    prop.key i
                    prop.className "leading-relaxed"
                    prop.children (renderInline t onLink onTag)
                ])

    Html.div [ prop.className "max-w-none"; prop.children blocks ]
