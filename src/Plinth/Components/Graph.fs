/// The Firmament — in the old sense: the vault of heaven. Your vault,
/// drawn overhead as a living star map. A hand-rolled force-directed
/// layout on a raw canvas — no chart library, in keeping with the Zero
/// Plugins pillar. Notes are stars, [[links]] are the lines between
/// them, daily notes burn amber, and broken links wait as dim unborn
/// stars until someone writes them.
module Plinth.Components.Graph

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom
open Browser.Types
open Feliz
open Plinth.Types

type FirmamentProps =
    { Data: GraphData
      Current: string option
      Dark: bool
      OnOpen: string -> unit
      OnClose: unit -> unit }

/// One star's mutable simulation state. Mutation is deliberate: the
/// physics loop touches every star every frame, far from React's eyes.
type private Star =
    { Name: string
      Exists: bool
      IsDaily: bool
      Degree: int
      Phase: float
      mutable X: float
      mutable Y: float
      mutable Vx: float
      mutable Vy: float }

/// Camera + interaction state, likewise mutated inside the loop.
type private View =
    { mutable Scale: float
      mutable OffX: float
      mutable OffY: float
      mutable Alpha: float
      mutable Hover: int
      mutable Drag: int
      mutable Panning: bool
      mutable LastX: float
      mutable LastY: float
      mutable Moved: float }

let private dailyRe = Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2}$")

/// Cheap deterministic hash so twinkle phases and jitter are stable
/// across reopenings instead of reshuffling every time.
let private hash (s: string) =
    let mutable h = 0.0
    for c in s do
        h <- (h * 31.0 + float (int c)) % 1000003.0
    h

let private radiusOf (degree: int) = 3.0 + sqrt (float degree) * 1.9 |> min 13.0

[<ReactComponent>]
let Firmament (props: FirmamentProps) =
    let canvasRef = React.useElementRef ()

    // Everything lives for as long as the overlay does; the effect owns
    // the simulation, the render loop, and every event listener.
    let setup () : unit -> unit =
        match canvasRef.current with
        | None -> ignore
        | Some el ->
            let canvas = el :?> HTMLCanvasElement
            let ctx = canvas.getContext_2d ()

            // ---- palette -------------------------------------------------
            let bg, speck, edgeCol, edgeHot, noteFill, dailyFill, ghostCol, labelCol, ringCol =
                if props.Dark then
                    "#0c0a09",
                    "rgba(255,255,255,",
                    "rgba(52,211,153,0.16)",
                    "rgba(251,191,36,0.85)",
                    "#34d399",
                    "#fbbf24",
                    "#78716c",
                    "rgba(214,211,209,",
                    "#6ee7b7"
                else
                    "#fafaf9",
                    "rgba(87,83,78,",
                    "rgba(6,95,70,0.16)",
                    "rgba(180,83,9,0.85)",
                    "#065f46",
                    "#b45309",
                    "#a8a29e",
                    "rgba(68,64,60,",
                    "#059669"

            // ---- build the sky -------------------------------------------
            // Hubs get placed first on a golden-angle spiral so the most
            // connected notes settle near the middle of the sky.
            let order =
                props.Data.Nodes
                |> Array.mapi (fun i n -> i, n)
                |> Array.sortByDescending (fun (_, n) -> n.Degree)

            let stars = Array.zeroCreate<Star> props.Data.Nodes.Length

            order
            |> Array.iteri (fun rank (originalIdx, n) ->
                let angle = float rank * 2.39996 + hash n.Name % 1.0
                let r = 55.0 * sqrt (float rank + 0.5)

                stars.[originalIdx] <-
                    { Name = n.Name
                      Exists = n.Exists
                      IsDaily = dailyRe.IsMatch n.Name
                      Degree = n.Degree
                      Phase = hash n.Name % 6.28
                      X = cos angle * r
                      Y = sin angle * r
                      Vx = 0.0
                      Vy = 0.0 })

            let index = Collections.Generic.Dictionary<string, int>()
            props.Data.Nodes |> Array.iteri (fun i n -> index.[n.Name.ToLowerInvariant()] <- i)

            let edges =
                props.Data.Edges
                |> Array.choose (fun e ->
                    match
                        index.TryGetValue(e.Source.ToLowerInvariant()),
                        index.TryGetValue(e.Target.ToLowerInvariant())
                    with
                    | (true, a), (true, b) -> Some(a, b)
                    | _ -> None)

            let neighbors = Array.init stars.Length (fun _ -> Collections.Generic.HashSet<int>())

            for a, b in edges do
                neighbors.[a].Add b |> ignore
                neighbors.[b].Add a |> ignore

            let currentIdx =
                match props.Current with
                | Some name ->
                    match index.TryGetValue(name.ToLowerInvariant()) with
                    | true, i -> i
                    | _ -> -1
                | None -> -1

            let view =
                { Scale = 1.0
                  OffX = 0.0
                  OffY = 0.0
                  Alpha = 1.0
                  Hover = -1
                  Drag = -1
                  Panning = false
                  LastX = 0.0
                  LastY = 0.0
                  Moved = 0.0 }

            // Start roughly fitted: the spiral's outer radius vs. viewport.
            let fit () =
                let w = canvas.clientWidth
                let h = canvas.clientHeight
                let outer = 70.0 * sqrt (float stars.Length + 0.5) * 1.35 + 90.0
                view.Scale <- min 1.25 (min w h / 2.0 / outer) |> max 0.18
                view.OffX <- w / 2.0
                view.OffY <- h / 2.0

            fit ()

            // ---- physics -------------------------------------------------
            let step () =
                view.Alpha <- view.Alpha * 0.985

                let a = view.Alpha
                let n = stars.Length

                for i in 0 .. n - 1 do
                    let s = stars.[i]
                    // Gentle pull toward the origin keeps lone stars home.
                    s.Vx <- s.Vx - s.X * 0.005 * a
                    s.Vy <- s.Vy - s.Y * 0.005 * a

                    for j in i + 1 .. n - 1 do
                        let o = stars.[j]
                        let dx = s.X - o.X
                        let dy = s.Y - o.Y
                        let d2 = dx * dx + dy * dy + 60.0
                        let f = 4200.0 / d2 * a
                        let d = sqrt d2
                        let fx = dx / d * f
                        let fy = dy / d * f
                        s.Vx <- s.Vx + fx
                        s.Vy <- s.Vy + fy
                        o.Vx <- o.Vx - fx
                        o.Vy <- o.Vy - fy

                for src, tgt in edges do
                    let s = stars.[src]
                    let t = stars.[tgt]
                    let dx = t.X - s.X
                    let dy = t.Y - s.Y
                    let d = sqrt (dx * dx + dy * dy) + 0.01
                    let f = (d - 118.0) * 0.014 * a
                    let fx = dx / d * f
                    let fy = dy / d * f
                    s.Vx <- s.Vx + fx
                    s.Vy <- s.Vy + fy
                    t.Vx <- t.Vx - fx
                    t.Vy <- t.Vy - fy

                for i in 0 .. n - 1 do
                    let s = stars.[i]

                    if i = view.Drag then
                        s.Vx <- 0.0
                        s.Vy <- 0.0
                    else
                        s.Vx <- s.Vx * 0.86
                        s.Vy <- s.Vy * 0.86
                        s.X <- s.X + s.Vx
                        s.Y <- s.Y + s.Vy

                // Keep the settling constellation centred under the camera —
                // but never while the user is holding a star or the sky.
                if view.Drag < 0 && not view.Panning && n > 0 then
                    let mutable cx = 0.0
                    let mutable cy = 0.0

                    for s in stars do
                        cx <- cx + s.X
                        cy <- cy + s.Y

                    cx <- cx / float n
                    cy <- cy / float n

                    for s in stars do
                        s.X <- s.X - cx
                        s.Y <- s.Y - cy

            // ---- drawing -------------------------------------------------
            let draw (t: float) =
                let dpr = window.devicePixelRatio
                let w = canvas.clientWidth
                let h = canvas.clientHeight

                if canvas.width <> w * dpr || canvas.height <> h * dpr then
                    canvas.width <- w * dpr
                    canvas.height <- h * dpr

                // Fixed backdrop, screen space: night (or day) sky specks.
                ctx.setTransform (dpr, 0.0, 0.0, dpr, 0.0, 0.0)
                ctx.fillStyle <- !^bg
                ctx.fillRect (0.0, 0.0, w, h)

                for k in 0 .. 139 do
                    let sx = float ((k * 73 + 17) % 997) / 997.0 * w
                    let sy = float ((k * 179 + 31) % 991) / 991.0 * h
                    let tw = 0.10 + 0.07 * sin (t / 1400.0 + float k)
                    ctx.fillStyle <- !^(speck + string tw + ")")
                    ctx.beginPath ()
                    ctx.arc (sx, sy, 0.5 + float (k % 3) * 0.35, 0.0, 6.2832)
                    ctx.fill ()

                // World space from here on.
                ctx.setTransform (dpr * view.Scale, 0.0, 0.0, dpr * view.Scale, dpr * view.OffX, dpr * view.OffY)

                // Focus set: hovering a star spotlights its neighborhood.
                let focused =
                    if view.Hover >= 0 then
                        let set = Collections.Generic.HashSet<int>(neighbors.[view.Hover])
                        set.Add view.Hover |> ignore
                        Some set
                    else
                        None

                let dimmed i =
                    match focused with
                    | Some set -> not (set.Contains i)
                    | None -> false

                ctx.lineWidth <- 1.0 / view.Scale

                for src, tgt in edges do
                    let hot =
                        view.Hover >= 0 && (src = view.Hover || tgt = view.Hover)

                    ctx.strokeStyle <- !^(if hot then edgeHot else edgeCol)
                    ctx.globalAlpha <- if not hot && focused.IsSome then 0.25 else 1.0
                    ctx.lineWidth <- (if hot then 1.6 else 1.0) / view.Scale
                    ctx.beginPath ()
                    ctx.moveTo (stars.[src].X, stars.[src].Y)
                    ctx.lineTo (stars.[tgt].X, stars.[tgt].Y)
                    ctx.stroke ()

                ctx.globalAlpha <- 1.0

                for i in 0 .. stars.Length - 1 do
                    let s = stars.[i]
                    let r = radiusOf s.Degree
                    let twinkle = 0.78 + 0.22 * sin (t / 700.0 + s.Phase)
                    ctx.globalAlpha <- (if dimmed i then 0.13 else twinkle)

                    if s.Exists then
                        ctx.fillStyle <- !^(if s.IsDaily then dailyFill else noteFill)
                        ctx.beginPath ()
                        ctx.arc (s.X, s.Y, r, 0.0, 6.2832)
                        ctx.fill ()
                    else
                        // Unborn: a dim dashed outline where a note could be.
                        ctx.strokeStyle <- !^ghostCol
                        ctx.lineWidth <- 1.2 / view.Scale
                        ctx.setLineDash [| 3.0 / view.Scale; 3.0 / view.Scale |]
                        ctx.beginPath ()
                        ctx.arc (s.X, s.Y, max 3.5 (r * 0.8), 0.0, 6.2832)
                        ctx.stroke ()
                        ctx.setLineDash [||]

                    // The open note pulses so you can find yourself in the sky.
                    if i = currentIdx then
                        ctx.globalAlpha <- 0.9
                        ctx.strokeStyle <- !^ringCol
                        ctx.lineWidth <- 1.5 / view.Scale
                        ctx.beginPath ()
                        ctx.arc (s.X, s.Y, r + (3.5 + 1.5 * sin (t / 350.0)) / view.Scale, 0.0, 6.2832)
                        ctx.stroke ()

                    if i = view.Hover then
                        ctx.globalAlpha <- 1.0
                        ctx.strokeStyle <- !^(if props.Dark then "#fafaf9" else "#1c1917")
                        ctx.lineWidth <- 1.2 / view.Scale
                        ctx.beginPath ()
                        ctx.arc (s.X, s.Y, r + 3.0 / view.Scale, 0.0, 6.2832)
                        ctx.stroke ()

                // Labels: everything when zoomed in, hubs when zoomed out,
                // the focused neighborhood and current note always.
                ctx.font <- string (11.5 / view.Scale) + "px ui-sans-serif, system-ui, sans-serif"
                ctx.textAlign <- "left"

                for i in 0 .. stars.Length - 1 do
                    let s = stars.[i]

                    let show =
                        (not (dimmed i))
                        && (view.Scale >= 0.8
                            || s.Degree >= 3
                            || i = currentIdx
                            || (match focused with
                                | Some set -> set.Contains i
                                | None -> false))

                    if show then
                        let inFocus =
                            i = view.Hover || i = currentIdx

                        ctx.globalAlpha <- if inFocus then 0.95 else 0.6
                        ctx.fillStyle <- !^(labelCol + "1)")
                        ctx.fillText (s.Name, s.X + (radiusOf s.Degree + 5.0 / view.Scale), s.Y + 4.0 / view.Scale)

                ctx.globalAlpha <- 1.0

            // ---- loop ----------------------------------------------------
            let mutable raf = 0.0

            let rec loop (t: float) =
                if view.Alpha > 0.004 then
                    step ()

                draw t
                raf <- window.requestAnimationFrame loop

            raf <- window.requestAnimationFrame loop

            // ---- interaction ---------------------------------------------
            let toWorld (cx: float) (cy: float) =
                let rect = canvas.getBoundingClientRect ()
                (cx - rect.left - view.OffX) / view.Scale, (cy - rect.top - view.OffY) / view.Scale

            let hitTest (cx: float) (cy: float) =
                let wx, wy = toWorld cx cy
                let mutable best = -1
                let mutable bestD = infinity

                for i in 0 .. stars.Length - 1 do
                    let s = stars.[i]
                    let dx = s.X - wx
                    let dy = s.Y - wy
                    let d = sqrt (dx * dx + dy * dy)
                    let reach = radiusOf s.Degree + 6.0 / view.Scale

                    if d < reach && d < bestD then
                        best <- i
                        bestD <- d

                best

            let onMouseDown (e: Event) =
                let me = e :?> MouseEvent
                view.LastX <- me.clientX
                view.LastY <- me.clientY
                view.Moved <- 0.0
                let hit = hitTest me.clientX me.clientY

                if hit >= 0 then
                    view.Drag <- hit
                    view.Alpha <- max view.Alpha 0.25
                else
                    view.Panning <- true

            let onMouseMove (e: Event) =
                let me = e :?> MouseEvent
                let dx = me.clientX - view.LastX
                let dy = me.clientY - view.LastY
                view.Moved <- view.Moved + abs dx + abs dy
                view.LastX <- me.clientX
                view.LastY <- me.clientY

                if view.Drag >= 0 then
                    let wx, wy = toWorld me.clientX me.clientY
                    stars.[view.Drag].X <- wx
                    stars.[view.Drag].Y <- wy
                    view.Alpha <- max view.Alpha 0.25
                elif view.Panning then
                    view.OffX <- view.OffX + dx
                    view.OffY <- view.OffY + dy
                else
                    view.Hover <- hitTest me.clientX me.clientY

                    el?style?cursor <- (if view.Hover >= 0 then "pointer" else "grab")

            let onMouseUp (_: Event) =
                if view.Drag >= 0 && view.Moved < 5.0 then
                    props.OnOpen stars.[view.Drag].Name

                view.Drag <- -1
                view.Panning <- false

            let onWheel (e: Event) =
                e.preventDefault ()
                let we = e :?> WheelEvent
                let rect = canvas.getBoundingClientRect ()
                let mx = we.clientX - rect.left
                let my = we.clientY - rect.top
                let factor = exp (-we.deltaY * 0.0012)
                let next = view.Scale * factor |> max 0.12 |> min 4.0
                let applied = next / view.Scale
                view.OffX <- mx - (mx - view.OffX) * applied
                view.OffY <- my - (my - view.OffY) * applied
                view.Scale <- next

            let onKeyDown (e: Event) =
                let ke = e :?> KeyboardEvent

                if ke.key = "Escape" then
                    ke.preventDefault ()
                    props.OnClose ()

            canvas.addEventListener ("mousedown", onMouseDown)
            window.addEventListener ("mousemove", onMouseMove)
            window.addEventListener ("mouseup", onMouseUp)
            canvas.addEventListener ("wheel", onWheel)
            window.addEventListener ("keydown", onKeyDown)

            fun () ->
                window.cancelAnimationFrame raf
                canvas.removeEventListener ("mousedown", onMouseDown)
                window.removeEventListener ("mousemove", onMouseMove)
                window.removeEventListener ("mouseup", onMouseUp)
                canvas.removeEventListener ("wheel", onWheel)
                window.removeEventListener ("keydown", onKeyDown)

    React.useEffect (setup, [| box props.Data; box props.Dark |])

    let ghostCount =
        props.Data.Nodes |> Array.sumBy (fun n -> if n.Exists then 0 else 1)

    Html.div [
        prop.className "absolute inset-0 z-40 overflow-hidden bg-stone-50 dark:bg-stone-950"
        prop.children [
            Html.canvas [ prop.ref canvasRef; prop.className "block h-full w-full" ]
            // Chrome floats over the sky.
            Html.div [
                prop.className "pointer-events-none absolute inset-x-0 top-0 flex items-start justify-between p-4"
                prop.children [
                    Html.div [
                        prop.children [
                            Html.h2 [
                                prop.className "font-serif text-2xl font-bold text-emerald-900 dark:text-emerald-300"
                                prop.text "Firmament"
                            ]
                            Html.p [
                                prop.className "mt-0.5 text-xs text-stone-500 dark:text-stone-400"
                                prop.text (
                                    sprintf
                                        "%i notes · %i links%s"
                                        (props.Data.Nodes.Length - ghostCount)
                                        props.Data.Edges.Length
                                        (if ghostCount > 0 then
                                             sprintf " · %i unwritten" ghostCount
                                         else
                                             "")
                                )
                            ]
                        ]
                    ]
                    Html.button [
                        prop.className
                            "pointer-events-auto rounded px-2.5 py-1 text-lg leading-none text-stone-400 hover:bg-stone-200 hover:text-stone-600 dark:hover:bg-stone-800 dark:hover:text-stone-200"
                        prop.title "Close (Esc)"
                        prop.onClick (fun _ -> props.OnClose ())
                        prop.text "✕"
                    ]
                ]
            ]
            Html.div [
                prop.className
                    "pointer-events-none absolute inset-x-0 bottom-0 flex items-end justify-between p-4 text-[11px] text-stone-400 dark:text-stone-500"
                prop.children [
                    Html.div [
                        prop.className "flex flex-col gap-1"
                        prop.children [
                            Html.div [
                                prop.className "flex items-center gap-1.5"
                                prop.children [
                                    Html.span [
                                        prop.className "inline-block h-2 w-2 rounded-full bg-emerald-700 dark:bg-emerald-400"
                                    ]
                                    Html.span [ prop.text "note" ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex items-center gap-1.5"
                                prop.children [
                                    Html.span [
                                        prop.className "inline-block h-2 w-2 rounded-full bg-amber-600 dark:bg-amber-400"
                                    ]
                                    Html.span [ prop.text "daily note" ]
                                ]
                            ]
                            Html.div [
                                prop.className "flex items-center gap-1.5"
                                prop.children [
                                    Html.span [
                                        prop.className
                                            "inline-block h-2 w-2 rounded-full border border-dashed border-stone-400 dark:border-stone-500"
                                    ]
                                    Html.span [ prop.text "linked, not yet written" ]
                                ]
                            ]
                        ]
                    ]
                    Html.p [ prop.text "drag a star · scroll to zoom · drag the sky to pan · click a star to open it · esc to close" ]
                ]
            ]
        ]
    ]
