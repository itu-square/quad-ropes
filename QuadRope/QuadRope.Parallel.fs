namespace RadTrees.QuadRope

module Parallel =
    open RadTrees

    (* Constructor takes sub-ropes in order NE, NW, SW, SE. *)
    type ('a, 'b) Path =
        | Top
        | NW of 'a QuadRope * ('a, 'b) Path * 'a QuadRope * 'a QuadRope
        | NE of  ('a, 'b) Path * 'b QuadRope * 'a QuadRope * 'a QuadRope
        | SW of 'b QuadRope * 'b QuadRope * ('a, 'b) Path * 'a QuadRope
        | SE of 'b QuadRope * 'b QuadRope * 'b QuadRope * ('a, 'b) Path

    let down (rope, path) =
        match rope with
            | Node (_, _, _, ne, nw, sw, se) -> nw, NW (ne, path, sw, se)
            | _ -> rope, path

    let up (rope, path) =
        match path with
            | Top -> rope, path
            | NW (ne, path, sw, se) -> node ne rope sw se, path
            | NE (path, nw, sw, se) -> node rope nw sw se, path
            | SW (ne, nw, path, se) -> node ne nw rope se, path
            | SE (ne, nw, sw, path) -> node ne nw sw rope, path

    let rec upperLeftMost (rope, path) =
        match rope with
            | Empty
            | Leaf _ -> rope, path
            | _ -> upperLeftMost (down (rope, path))

    let start rope = upperLeftMost (rope, Top)

    type ('a, 'b) Progress =
        | More of 'a
        | Done of 'b

    /// Get the next leaf that follows the location (rope, path). It
    /// might seem unintuitive to process nodes in NW, NE, SW, SE
    /// order, but this allows us to re-use pseudo-constructors and
    /// other convenience functions.
    let rec next (rope, path : ('a, 'b) Path) =
        match path with
            | Top -> Done rope
            | NW (ne, path, sw, se) -> More (upperLeftMost (ne, NE (path, rope, sw, se)))
            | NE (path, nw, sw, se) -> More (upperLeftMost (sw, SW (rope, nw, path, se)))
            | SW (ne, nw, path, se) -> More (upperLeftMost (se, SE (ne, nw, rope, path)))
            | SE (ne, nw, sw, _)    -> next (node ne nw sw rope, path)

    /// Split the rope along the given path and return the processed
    /// and the unprocessed part. Here, it becomes clear why the NW,
    /// NE, SW, SE order is more desirable, as it allows us to use all
    /// the standard pseudo-constructors for quad ropes.
    let rec splitPath p u = function
        | Top -> p, u
        | NW (ne, path, sw, se) ->
            splitPath p (node ne u sw se) path
        | NE (path, nw, sw, se) ->
            splitPath (flatNode nw p) (thinNode u (flatNode sw se)) path
        | SW (ne, nw, path, se) ->
            splitPath (thinNode (flatNode nw ne) p) (flatNode u se) path
        | SE (ne, nw, sw, path) ->
            splitPath (node ne nw sw p) u path

    let rec zip (rope, path) =
        match path with
            | Top -> rope
            | _ -> zip (up (rope, path))

    let rec mapUntilSeq cond f (rope, path) =
        if cond() then
            More (rope, path)
        else
            let rope' = map f rope
            match next (rope', path) with
                | Done rope -> Done rope
                | More (rope, path) -> mapUntilSeq cond f (rope, path)

    let mapUntil cond f rope =
        let u, path = start rope
        match mapUntilSeq cond f (u, path) with
            | Done rope -> Done rope
            | More (u, path) -> More (splitPath Empty u path, path)

