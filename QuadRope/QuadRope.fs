// Copyright (c) 2016 Florian Biermann, fbie@itu.dk

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// * The above copyright notice and this permission notice shall be
//   included in all copies or substantial portions of the Software.

// * The software is provided "as is", without warranty of any kind,
//   express or implied, including but not limited to the warranties of
//   merchantability, fitness for a particular purpose and
//   noninfringement. In no event shall the authors or copyright holders be
//   liable for any claim, damages or other liability, whether in an action
//   of contract, tort or otherwise, arising from, out of or in connection
//   with the software or the use or other dealings in the software.

[<RequireQualifiedAccessAttribute>]
[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module RadTrees.QuadRope

open Types
open Utils

(* The maximal size of a leaf array in any direction. *)
#if DEBUG
let s_max = 4
#else
let s_max = 32
#endif

// Aliases for more concise code.
let inline rows qr = Types.rows qr
let inline cols qr = Types.cols qr
let inline depth qr = Types.depth qr
let inline isEmpty qr = Types.isEmpty qr

/// True if the quad rope contains a sparse sub-tree.
let rec isSparse = function
    | Sparse _ -> true
    | HCat (true, _, _, _, _, _) -> true
    | VCat (true, _, _, _, _, _) -> true
    | Slice ( _, _, _, _, qr) -> isSparse qr
    | _ -> false

/// Construct a Leaf if slc is non-empty. Otherwise, return Empty.
let leaf slc =
    if ArraySlice.length1 slc = 0 || ArraySlice.length2 slc = 0 then
        Empty
    else
        Leaf slc

/// Pseudo-constructor for initializing a new HCat node.
let internal hnode a b =
    match a, b with
        | Empty, _ -> b
        | _, Empty -> a
        | _ when rows a = rows b ->
            HCat (isSparse a || isSparse b, max (depth a) (depth b), rows a, cols a + cols b, a, b)
        | _ -> failwith "Cannot hcat nodes of different height."

/// Pseudo-constructor for initializing a new VCat node.
let internal vnode a b =
    match a, b with
        | Empty, _ -> b
        | _, Empty -> a
        | _ when cols a = cols b ->
            VCat (isSparse a || isSparse b, max (depth a) (depth b), rows a + rows b, cols a, a, b)
        | _ -> failwith "Cannot vcat nodes of different width."

let inline private withinRange qr i j =
    0 <= i && i < rows qr && 0 <= j && j < cols qr

let inline private checkBounds qr i j =
    if rows qr <= i then
        invalidArg "i" (sprintf "First index must be within bounds of quad rope: %d" i)
    if cols qr <= j then
        invalidArg "j" (sprintf "Second index must be within bounds of quad rope: %d" j)

/// Get the value of a location in the tree. This function does not
/// check whether i, j are within bounds.
let rec internal fastGet qr i j =
    match qr with
        | Empty -> invalidArg "qr" "Empty quad rope contains no values."
        | Leaf vs -> ArraySlice.get vs i j
        | HCat (_, _, _, _, a, b) ->
            if withinRange a i j then
                fastGet a i j
            else
                fastGet b i (j - cols a)
        | VCat (_, _, _, _, a, b) ->
            if withinRange a i j then
                fastGet a i j
            else
                fastGet b (i - rows a) j
        | Slice (x, y, _, _, qr) -> fastGet qr (i + x) (j + y)
        | Sparse (_, _, v) -> v

/// Get the value of a location in the tree.
let get root i j =
    checkBounds root i j
    fastGet root i j

/// Update a tree location without modifying the original tree.
let set root i j v =
    let rec set qr i j v =
        match qr with
            | Empty ->
                invalidArg "qr" "Empty quad rope cannot be set."
            | Leaf vs ->
                Leaf (ArraySlice.set vs i j v)
            | HCat (s, d, h, w, a, b) ->
                if withinRange a i j then
                    HCat (s, d, h, w, set a i j v, b)
                else
                    HCat (s, d, h, w, a, set b i (j - cols a) v)
            | VCat (s, d, h, w, a, b) ->
                if withinRange a i j then
                    VCat (s, d, h, w, set a i j v, b)
                else
                    VCat (s, d, h, w, a, set b (i - rows a) j v)
            // Set the underlying quad rope with offset.
            | Slice (x, y, h, w, qr) ->
                Slice (x, y, h, w, set qr (i + x) (j + y) v)
            // Initialize
            | Sparse (h, w, v') ->
                let vals = Array2D.create h w v'
                vals.[i, j] <- v
                leaf (ArraySlice.make vals)
    checkBounds root i j
    set root i j v

let private isBalanced d s =
    d < 45 && Fibonacci.fib (d + 2) <= s

/// True if rope is balanced horizontally. False otherwise.
let isBalancedH = function
    | HCat (_, d, _, w, _, _) -> isBalanced d w
    | VCat (_, d, _, w, _, _) -> isBalanced d w
    | _ -> true

/// True if rope is balanced vertically. False otherwise.
let isBalancedV = function
    | HCat (_, d, h, _, _, _) -> isBalanced d h
    | VCat (_, d, h, _, _, _) -> isBalanced d h
    | _ -> true

/// Calls <code>f</code> recursively on a list of quad ropes until the
/// list is reduced to a single quad rope.
let rec private reduceList f = function
    | []  -> Empty
    | [n] -> n
    | ns  -> reduceList f (f ns)

/// Rebuild one level of the original quad rope using
/// <code>merge</code> to merge adjacent quad ropes.
let rec private rebuild merge nodes =
    match nodes with
        | [] -> []
        | [x] -> [x]
        | x :: y :: nodes -> merge x y :: (rebuild merge nodes)

/// Balance rope horizontally.
let hbalance qr =
    let rec hbalance qr =
        if isBalancedH qr then
            qr
        else
            let rs = collect qr []
            reduceList (rebuild hnode) rs
    and collect qr rs  =
        match qr with
            | Empty -> rs
            | HCat (_, _, _, _, a, b) -> collect a (collect b rs)
            | VCat (_, _, _, _, a, b) ->
                vnode (hbalance a) (hbalance b) :: rs
            | _ -> qr :: rs
    hbalance qr

/// Balance rope vertically.
let vbalance qr =
    let rec vbalance qr =
        if isBalancedV qr then
            qr
        else
            let rs = collect qr []
            reduceList (rebuild vnode) rs
    and collect qr rs  =
        match qr with
            | Empty -> rs
            | HCat (_, _, _, _, a, b) ->
                hnode (vbalance a) (vbalance b) :: rs
            | VCat (_, _, _, _, a, b) ->
                collect a (collect b rs)
            | _ -> qr :: rs
    vbalance qr

/// Balancing after Boehm et al. It turns out that this is slightly
/// slower than reduce-rebuild balancing and the resulting tree is of
/// greater height.
module Boehm =
    let hbalance qr =
        let rec insert qr n = function
            | r0 :: r1 :: qrs when depth r1 <= n -> insert qr n (hnode r1 r0 :: qrs)
            | r0 :: qrs when depth r0 <= n -> (hnode r0 qr) :: qrs
            | qrs -> qr :: qrs
        let rec hbalance qr qrs =
            match qr with
                | Empty -> qrs
                | HCat (_, _, _, _, a, b) when not (isBalancedH qr) ->
                    hbalance b (hbalance a qrs)
                | _ ->
                    insert qr (Fibonacci.nth (cols qr)) qrs
        if isBalancedH qr then
            qr
        else
            List.reduce (fun ne nw -> hnode nw ne) (hbalance qr [])

    let vbalance qr =
        let rec insert qr n = function
            | r0 :: r1 :: qrs when depth r1 <= n -> insert qr n (vnode r1 r0 :: qrs)
            | r0 :: qrs when depth r0 <= n -> (vnode r0 qr) :: qrs
            | qrs -> qr :: qrs
        let rec vbalance qr qrs =
            match qr with
                | Empty -> qrs
                | VCat (_, _, _, _, a, b) when not (isBalancedV qr) ->
                    vbalance b (vbalance a qrs)
                | _ ->
                    insert qr (Fibonacci.nth (rows qr)) qrs
        if isBalancedV qr then
            qr
        else
            List.reduce (fun sw nw -> vnode nw sw) (vbalance qr [])

/// Compute the "subrope" starting from indexes i, j taking h and w
/// elements in vertical and horizontal direction.
let slice i j h w qr =
    // Negative indices are not allowed, otherwise clients could
    // extend the slice arbitrarily.
    let i0 = max 0 i
    let j0 = max 0 j
    let h0 = min (rows qr - i0) (max 0 h)
    let w0 = min (cols qr - j0) (max 0 w)
    match qr with
        // Slicing on Empty has no effect.
        | Empty -> Empty
        // Index domain of quad rope is not inside bounds.
        | _ when rows qr = i0 || cols qr = j0 || h0 <= 0 || w0 <= 0 ->
            Empty
        // Index domain of quad rope is entirely inside bounds.
        | _ when i0 = 0 && rows qr <= h0 && j0 = 0 && cols qr <= w0 ->
            qr
        // Leaves are sliced on ArraySlice level, saves one level of
        // indirection.
        | Leaf vals ->
            leaf (ArraySlice.slice i0 j0 h0 w0 vals)
        // Avoid directly nested slices, unpack the original slice.
        | Slice (i1, j1, _, _, qr) ->
            Slice (i0 + i1, j0 + j1, h0, w0, qr)
        // Just re-size the sparse quad rope.
        | Sparse (_, _, v) ->
            Sparse (h0, w0, v)
        // Just initialize a new slice.
        | _ ->
            Slice (i0, j0, h0, w0, qr)

/// Split rope vertically from row i, taking h rows.
let inline vslice i h qr =
    slice i 0 h (cols qr) qr

/// Split rope horizontally from column j, taking w columns.
let inline hslice j w qr =
    slice 0 j (rows qr) w qr

/// Split rope in two at row i.
let inline vsplit2 qr i =
    vslice 0 i qr, vslice i (rows qr) qr

/// Split rope in two at column j.
let inline hsplit2 qr j =
    hslice 0 j qr, hslice j (cols qr) qr

/// Split a quad rope in four quadrants that differ by at most one row
/// and column in size. Return order is HCat (VCat (a, b), VCat (c, d)).
let inline split4 qr =
    slice 0               0               (rows qr >>> 1) (cols qr >>> 1) qr,
    slice (rows qr >>> 1) 0               (rows qr)       (cols qr >>> 1) qr,
    slice 0               (cols qr >>> 1) (rows qr >>> 1) (cols qr)       qr,
    slice (rows qr >>> 1) (cols qr >>> 1) (rows qr)       (cols qr)       qr

// Get a single row or column
let row qr i = slice i 0 1 (cols qr) qr
let col qr j = slice 0 j (rows qr) 1 qr

// Get a sequence of rows or columns
let toRows qr = Seq.init (rows qr) (row qr)
let toCols qr = Seq.init (cols qr) (col qr)

/// Initialize a rope where all elements are <code>v</code>.
let inline create h w v =
    if h <= 0 || w <= 0 then
        Empty
    else
        Sparse (h, w, v)

/// Materialize a quad rope slice, i.e. traverse the slice and
/// allocate new quad rope nodes and new slices on arrays. Does not
/// allocate new arrays.
let materialize qr =
    let rec materialize i j h w qr =
        match qr with
            | _ when i <= 0 && j <= 0 && rows qr <= h && cols qr <= w ->
                qr
            | _ when rows qr <= i || cols qr <= j || h <= 0 || w <= 0 ->
                Empty
            | Empty ->
                Empty
            | Leaf vs ->
                leaf (ArraySlice.slice i j h w vs)
            | HCat (_, _, _, _, a, b) ->
                let a' = materialize i j h w a
                let b' = materialize i (j - cols a) h (w - cols a') b
                hnode a' b'
            | VCat (_, _, _, _, a, b) ->
                let a' = materialize i j h w a
                let b' = materialize (i - rows a) j (h - rows a') w b
                vnode a' b'
            | Slice (x, y, r, c, qr) ->
                materialize (i + x) (j + y) (min r h) (min c w) qr
            | Sparse (h', w', v) ->
                // We replicate the code for computing indices from slice here.
                create (min h (h' - (max 0 i))) (min w (w' - (max 0 j))) v
    match qr with
        | Slice (i, j, h, w, qr) -> materialize i j h w qr
        | qr -> qr

/// Concatenate two trees vertically. For the sake of leave size, this
/// may result in actually keeping parts of a large area in memory
/// twice. TODO: Consider other options.
let vcat a b =
    let canMerge a b =
        ArraySlice.cols a = ArraySlice.cols b && ArraySlice.rows a + ArraySlice.rows b <= s_max
    let rec vcat a b =
        match a, b with
            // Concatenation with Empty yields argument.
            | Empty, _ -> b
            | _, Empty -> a

            // Merge single leaves.
            | Leaf us, Leaf ls when canMerge us ls ->
                leaf (ArraySlice.cat1 us ls)

            // Merge first-level leaves.
            | VCat (_, _, _, _, aa, Leaf ab), Leaf b when canMerge ab b ->
                vnode aa (leaf (ArraySlice.cat1 ab b))
            | Leaf a, VCat (_, _, _, _, Leaf ba, bb) when canMerge a ba ->
                vnode (leaf (ArraySlice.cat1 a ba)) bb

            | Sparse (h1, w1, v1), Sparse (h2, w2, v2) when w1 = w2 && v1 = v2 ->
                Sparse (h1 + h2, w1, v1)

            // TODO: Further optimizations?

            // Create a new node pointing to arguments.
            | _ -> vnode a b

    if (not ((isEmpty a) || (isEmpty b))) && cols a <> cols b then
        invalidArg "b" "B quad rope must be of same width as a quad rope."
    else
        vbalance (vcat a b)

/// Concatenate two trees horizontally. For the sake of leave size,
/// this may result in actually keeping parts of a large area in
/// memory twice. TODO: Consider other options.
let hcat a b =
    let canMerge a b =
        ArraySlice.rows a = ArraySlice.rows b && ArraySlice.cols a + ArraySlice.cols b <= s_max
    let rec hcat a b =
        match a, b with
            // Concatenation with Empty yields argument.
            | Empty, _ -> b
            | _, Empty -> a

            // Merge single leaves.
            | Leaf ls, Leaf rs when canMerge ls rs ->
                leaf (ArraySlice.cat2 ls rs)

            // Merge sub-leaves.
            | HCat (_, _, _, _, aa, Leaf ab), Leaf b when canMerge ab b ->
                hnode aa (leaf (ArraySlice.cat2 ab b))
            | Leaf a, HCat (_, _, _, _, Leaf ba, bb) when canMerge a ba ->
                hnode (leaf (ArraySlice.cat2 a ba)) bb

            | Sparse (h1, w1, v1), Sparse (h2, w2, v2) when h1 = h2 && v1 = v2 ->
                Sparse (h1, w1 + w2, v1)

            // TODO: Further optimizations?

            // Create a new node pointing to arguments.
            | _ -> hnode a b

    if (not ((isEmpty a) || (isEmpty b))) && rows a <> rows b then
        invalidArg "b" "B quad rope must be of same height as a quad rope."
    else
        hbalance (hcat a b)

/// Reverse rope horizontally.
let hrev qr =
    let rec hrev qr tgt =
        match qr with
            | Leaf slc ->
                leaf (Target.hrev slc tgt)
            | HCat (_, _, _, _, a, b) ->
                hnode (hrev b (Target.incrementCol tgt (cols a))) (hrev a tgt)
            | VCat (_, _, _, _, a, b) ->
                vnode (hrev a tgt) (hrev b (Target.incrementRow tgt (rows a)))
            | Slice _ ->
                hrev (materialize qr) tgt
            | _ -> qr
    hrev qr (Target.make (rows qr) (cols qr))

/// Reverse rope vertically.
let vrev qr =
    let rec vrev qr tgt =
        match qr with
            | Leaf slc ->
                leaf (Target.vrev slc tgt)
            | HCat (_, _, _, _, a, b) ->
                hnode (vrev a tgt) (vrev b (Target.incrementCol tgt (cols a)))
            | VCat (_, _, _, _, a, b) ->
                vnode (vrev b (Target.incrementRow tgt (rows a))) (vrev a tgt)
            | Slice _ ->
                vrev (materialize qr) tgt
            | _ -> qr
    vrev qr (Target.make (rows qr) (cols qr))

/// Create a quad rope from an array slice instance.
let internal fromArraySlice slc =
    /// Build nodes by splitting underlying slices horizontally.
    let rec hsplit slc =
        if ArraySlice.rows slc <= 0 || ArraySlice.cols slc <= 0 then
            Empty
        else if ArraySlice.cols slc <= s_max && ArraySlice.rows slc <= s_max then
            leaf slc
        else if ArraySlice.cols slc <= s_max then
            vsplit slc
        else
            let a, b = ArraySlice.hsplit2 slc
            hnode (vsplit a) (vsplit b)
    /// Build nodes by splitting underlying slices vertically.
    and vsplit slc =
        if ArraySlice.rows slc <= 0 || ArraySlice.cols slc <= 0 then
            Empty
        else if ArraySlice.cols slc <= s_max && ArraySlice.rows slc <= s_max then
            leaf slc
        else if ArraySlice.rows slc <= s_max then
            hsplit slc
        else
            let a, b = ArraySlice.vsplit2 slc
            vnode (hsplit a) (hsplit b)
    hsplit slc

/// Initialize a rope from a native 2D-array.
let fromArray2D arr =
    fromArraySlice (ArraySlice.make arr)

/// Generate a new tree without any intermediate values.
let inline init h w f =
    fromArray2D (Array2D.init h w f)

/// Generate a singleton quad rope.
let inline singleton v =
    create 1 1 v

/// True if rope is a singleton, false otherwise.
let isSingleton qr =
    rows qr = 1 && cols qr = 1

/// Initialize a rope from a native array for a given width.
let fromArray vs w =
    if Array.length vs % w <> 0 then
        invalidArg "w" "Must be evenly divisible by array length."
    let h = Array.length vs / w
    init h w (fun i j -> vs.[i * w + j])

/// Apply a function with side effects to all elements of the rope.
let rec iter f = function
    | Empty -> ()
    | Leaf vs -> ArraySlice.iter f vs
    | HCat (_, _, _, _, a, b) ->
        iter f a
        iter f b
    | VCat (_, _, _, _, a, b) ->
        iter f a
        iter f b
    | Slice _ as qr -> iter f (materialize qr)
    | Sparse (h, w, v) ->
        for i in 1 .. h * w do
            f v

/// Apply a function with side effects to all elements and their
/// corresponding index pair.
let iteri f qr =
    let rec iteri f i j = function
        | Empty -> ()
        | Leaf vs -> ArraySlice.iteri (fun i0 j0 v -> f (i + i0) (j + j0) v) vs
        | HCat (_, _, _, _, a, b) ->
            iteri f i j a
            iteri f i (j + cols a) b
        | VCat (_, _, _, _, a, b) ->
            iteri f i j a
            iteri f (i + rows a) j b
        | Slice _ as qr -> iteri f i j (materialize qr)
        | Sparse (h, w, v) ->
            for r in i .. i + h - 1 do
                for c in j .. j + w - 1 do
                    f r c v
    iteri f 0 0 qr

/// Conversion into 1D array.
let toArray qr =
    let arr = Array.zeroCreate (rows qr * cols qr)
    // This avoids repeated calls to get; it is enough to traverse
    // the rope once.
    iteri (fun i j v -> arr.[i * cols qr + j] <- v) qr
    arr

let toRowsArray qr =
    toRows qr |> Seq.map toArray |> Seq.toArray

let toColsArray qr =
    toCols qr |> Seq.map toArray |> Seq.toArray

/// Conversion into 2D array.
let toArray2D qr =
    let arr = Array2D.zeroCreate (rows qr) (cols qr)
    // This avoids repeated calls to get; it is enough to traverse
    // the rope once.
    iteri (fun i j v -> arr.[i, j] <- v) qr
    arr

/// Reallocate a rope form the ground up. Sometimes, this is the
/// only way to improve performance of a badly composed quad rope.
let inline reallocate qr =
    fromArray2D (toArray2D qr)

/// Apply a function to every element in the tree.
let map f qr =
    let rec map qr tgt =
        match qr with
            | Empty ->
                Empty
            // Initialize target as soon as quad rope is dense.
            | _ when Target.isEmpty tgt && not (isSparse qr) ->
                map qr (Target.make (rows qr) (cols qr))
            // Write into target and construct new leaf.
            | Leaf slc ->
                Leaf (Target.map f slc tgt)
            // Adjust target descriptor horizontally.
            | HCat (_, _, _, _, a, b) ->
                hnode (map a tgt) (map b (Target.incrementCol tgt (cols a)))
            // Adjust target descriptor vertically.
            | VCat (_, _, _, _, a, b) ->
                vnode (map a tgt) (map b (Target.incrementRow tgt (rows a)))
            // Materialize quad rope and then map.
            | Slice _ ->
                map (materialize qr) tgt
            // The target is empty, don't write.
            | Sparse (h, w, v) ->
                Sparse (h, w, f v)
    map qr Target.empty

/// Map a function f to each row of the quad rope.
let hmap f qr =
    init (rows qr) 1 (fun i _ -> slice i 0 1 (cols qr) qr |> f)

/// Map a function f to each column of the quad rope.
let vmap f qr =
    init 1 (cols qr) (fun _ j -> slice 0 j (rows qr) 1 qr |> f)

/// Zip implementation for the general case where we do not assume
/// that both ropes have the same internal structure.
let rec internal genZip f lqr rqr tgt =
    match rqr with
        | Sparse (_, _, v) -> map (fun x -> f x v) lqr
        | _ ->
            match lqr with
                | Empty -> Empty

                | _ when Target.isEmpty tgt && (not (isSparse lqr) || not (isSparse rqr)) ->
                    genZip f lqr rqr (Target.make (rows lqr) (cols lqr))

                | Leaf slc ->
                    // Able to call map
                    match materialize rqr with
                        | Sparse (_, _, v') -> leaf (Target.map (fun v -> f v v') slc tgt)
                        | rqr -> leaf (Target.mapi (fun i j v -> f v (fastGet rqr i j)) slc tgt)

                | HCat (_, _, _, _, aa, ab) ->
                    let ba, bb = hsplit2 rqr (cols aa)
                    hnode (genZip f aa ba tgt) (genZip f ab bb (Target.incrementCol tgt (cols aa)))

                | VCat (_, _, _, _, aa, ab) ->
                    let ba, bb = vsplit2 rqr (rows aa)
                    vnode (genZip f aa ba tgt) (genZip f ab bb (Target.incrementRow tgt (rows aa)))

                | Slice _ -> genZip f (materialize lqr) rqr tgt
                | Sparse (_, _, v) -> map (f v) rqr // lqr is sparse, hence tgt must be empty.

/// True if the shape of two ropes match.
let shapesMatch a b =
    rows a = rows b && cols a = cols b

/// Zip function that assumes that the internal structure of two ropes
/// matches. If not, it falls back to the slow, general case.
let rec internal fastZip f lqr rqr tgt =
    match lqr, rqr with
        | Empty, Empty -> Empty

        // Initialize target if any of the two quad ropes is dense.
        | _ when Target.isEmpty tgt && (not (isSparse lqr) || not (isSparse rqr)) ->
            fastZip f lqr rqr (Target.make (rows lqr) (cols lqr))

        | Leaf ls, Leaf rs when shapesMatch lqr rqr ->
            leaf (Target.map2 f ls rs tgt)

        | HCat (_, _, _, _, aa, ab), HCat (_, _, _, _, ba, bb)
            when shapesMatch aa ba && shapesMatch ab bb ->
                hnode (fastZip f aa ba tgt) (fastZip f ab bb (Target.incrementCol tgt (cols aa)))

        | VCat (_, _, _, _, aa, ab), VCat (_, _, _, _, ba, bb)
            when shapesMatch aa ba && shapesMatch ab bb ->
                vnode (fastZip f aa ba tgt) (fastZip f ab bb (Target.incrementRow tgt (rows aa)))

        // It may pay off to reallocate first if both reallocated quad
        // ropes have the same internal shape. This might be
        // over-fitted to matrix multiplication.
        | Slice _, Slice _ -> fastZip f (materialize lqr) (materialize rqr) tgt
        | Slice _, _ ->       fastZip f (materialize lqr) rqr tgt
        | Slice _, Slice _ -> fastZip f lqr (materialize rqr) tgt

        // Sparse branches can be reduced to either a single call to f
        // in case both arguments are sparse, or to a mapping f.
        | Sparse (h, w, v1), Sparse (_, _, v2) -> Sparse (h, w, f v1 v2)
        | Sparse (_, _, v), _ -> map (f v) rqr
        | _, Sparse (_, _, v) -> map (fun x -> f x v) lqr

        // Fall back to general case.
        | _ -> genZip f lqr rqr tgt

/// Apply f to each (i, j) of lqr and rope.
let zip f lqr rqr =
    if not (shapesMatch lqr rqr) then
        failwith "Quad ropes must have the same shape."
    fastZip f lqr rqr Target.empty

/// Apply f to all values of the rope and reduce the resulting values
/// to a single scalar using g. Variable epsilon is the neutral
/// element for g. We assume that g epsilon x = g x epsilon = x.
let rec mapreduce f g epsilon = function
    | Empty -> epsilon
    | Leaf slc ->
        ArraySlice.mapreduce f g slc
    | HCat (_, _, _, _, a, b) ->
        g (mapreduce f g epsilon a) (mapreduce f g epsilon b)
    | VCat (_, _, _, _, a, b) ->
        g (mapreduce f g epsilon a) (mapreduce f g epsilon b)
    | Slice _ as qr ->
        mapreduce f g epsilon (materialize qr)
    | Sparse (h, w, v) ->
        let fv = f v
        if fv = epsilon then
            epsilon
        else
            let mutable acc = f v
            for i in 2 .. h * w do
                acc <- g acc fv
            acc

/// Reduce all values of the rope to a single scalar.
let reduce f epsilon qr = mapreduce id f epsilon qr

let hmapreduce f g epsilon qr = hmap (mapreduce f g epsilon) qr
let vmapreduce f g epsilon qr = vmap (mapreduce f g epsilon) qr

let hreduce f epsilon qr = hmapreduce id f epsilon qr
let vreduce f epsilon qr = vmapreduce id f epsilon qr

/// Compute the row-wise prefix sum of the rope for f starting with
/// states.
let rec hscan f states = function
    | Empty -> Empty
    | Leaf vs -> Leaf (ArraySlice.scan2 f states vs)
    | HCat (_, _, _, _, a, b) ->
        let a' = hscan f states a
        let b' = hscan f (fun i -> get a' i (cols a' - 1)) b
        hnode a' b'
    | VCat (_, _, _, _, a, b) ->
        // Offset states function by rows of a; recursion does not
        // maintain index offset.
        vnode (hscan f states a) (hscan f (((+) (rows a)) >> states) b)
    | Slice _ as qr -> hscan f states (materialize qr)
    | Sparse (h, w, v) ->
        hscan f states (init h w (fun _ _ -> v))

/// Compute the column-wise prefix sum of the rope for f starting
/// with states.
let rec vscan f states = function
    | Empty -> Empty
    | Leaf vs -> Leaf (ArraySlice.scan1 f states vs)
    | HCat (_, _, _, _, a, b) ->
        // Offset states function by cols of a; recursion does not
        // maintain index offset.
        hnode (vscan f states a) (vscan f (((+) (cols a)) >> states) b)
    | VCat (_, _, _, _, a, b) ->
        let a' = vscan f states a
        let b' = vscan f (get a' (rows a' - 1)) b
        vnode a' b'
    | Slice _ as qr -> vscan f states (materialize qr)
    | Sparse (h, w, v) ->
         vscan f states (init h w (fun _ _ -> v))

/// Compute the generalized summed area table for functions plus and
/// minus; all rows and columns are initialized with init.
let scan plus minus init qr =
    // Prefix is implicitly passed on through side effects when
    // writing into tgt.
    let rec scan pre qr tgt =
        match qr with
            | Empty -> Empty
            | Leaf slc ->
                Target.scan plus minus pre slc tgt
                Leaf (Target.toSlice tgt (ArraySlice.rows slc) (ArraySlice.cols slc))

            // Scanning b depends on scanning a; a resides "above" b.
            | HCat (_, _, _, _, a, b) ->
                let a' = scan pre a tgt
                let tgt' = Target.incrementCol tgt (cols a)
                let b' = scan (Target.get tgt') b tgt'
                hnode a' b'

            // Scanning b depends on scanning a; a resides "left of" b.
            | VCat (_, _, _, _, a, b) ->
                let a' = scan pre a tgt
                let tgt' = Target.incrementRow tgt (rows a)
                let b' = scan (Target.get tgt') b tgt'
                vnode a' b'

            | Slice _ -> scan pre (materialize qr) tgt

            | Sparse (h, w, v) ->
                // Fill the target imperatively.
                Target.fill tgt h w v
                // Get a slice over the target.
                let slc = Target.toSlice tgt h w
                // Compute prefix imperatively.
                Target.scan plus minus pre slc tgt
                // Make a new quad rope from array slice.
                fromArraySlice slc

    let tgt = Target.makeWithFringe (rows qr) (cols qr) init
    scan (Target.get tgt) qr tgt

/// Checks that some relation p holds between each two adjacent
/// elements in each row. This is slow and should not really be
/// used.
let forallRows p = function
    | Empty -> true
    | qr ->
        hmapreduce List.singleton List.append [] qr
        |> map List.pairwise
        |> mapreduce (List.forall (fun (a, b) -> p a b)) (&&) true

/// Checks that some relation p holds between each two adjacent
/// elements in each column. This is slow and should not really be
/// used.
let forallCols p = function
    | Empty -> true
    | qr ->
        vmapreduce List.singleton List.append [] qr
        |> map List.pairwise
        |> mapreduce (List.forall (fun (a, b) -> p a b)) (&&) true

/// Apply predicate p to all elements of qr and reduce the
/// elements in both dimension using logical and.
let forall p qr = mapreduce p (&&) true qr

/// Apply predicate p to all elements of qr and reduce the
/// elements in both dimensions using logical or.
let exists p qr = mapreduce p (||) false qr

/// Remove all elements from rope for which p does not hold. Input
/// rope must be of height 1.
let rec hfilter p = function
    | Empty -> Empty
    | Leaf slc -> leaf (ArraySlice.filter2 p slc)
    | HCat (_, _, 1, _, a, b) ->
        hnode (hfilter p a) (hfilter p b)
    | Sparse (1, _, v) as qr when p v -> qr
    | Sparse (1, _, _) -> Empty
    | _ -> failwith "Quad rope height must be exactly one."

/// Remove all elements from rope for which p does not hold. Input
/// rope must be of width 1.
let rec vfilter p = function
    | Empty -> Empty
    | Leaf slc -> leaf (ArraySlice.filter1 p slc)
    | VCat (_, _, _, 1, a, b) ->
        vnode (vfilter p a) (vfilter p b)
    | Sparse (_, 1, v) as qr when p v -> qr
    | Sparse (_, 1, _) -> Empty
    | _ -> failwith "Quad rope width must be exactly one."

/// Transpose the quad rope. This is equal to swapping indices,
/// such that get qr i j = get (transpose qr) j i.
let transpose qr =
    let rec transpose qr tgt =
        match qr with
            | Empty -> Empty
            | Leaf slc ->
                leaf (Target.transpose slc tgt)
            | HCat (_, _, _, _, a, b) ->
                vnode (transpose a tgt) (transpose b (Target.incrementCol tgt (cols a)))
            | VCat (_, _, _, _, a, b) ->
                hnode (transpose a tgt) (transpose b (Target.incrementRow tgt (rows a)))
            | Slice _ ->
                transpose (materialize qr) tgt
            | Sparse (h, w, v) -> Sparse (w, h, v)
    transpose qr (Target.make (cols qr) (rows qr))

/// Compare two quad ropes element wise and return true if they are
/// equal. False otherwise.
let equals qr0 qr1 =
    reduce (&&) true (zip (=) qr0 qr1)

/// Replace branches of equal values with sparse representations.
let rec compress = function
    | Leaf slc when ArraySlice.mapreduce ((=) (ArraySlice.get slc 0 0)) (&&) slc ->
        create (ArraySlice.rows slc) (ArraySlice.cols slc) (ArraySlice.get slc 0 0)
    | HCat (_, _, _, _, a, b) ->
        hcat (compress a) (compress b)
    | VCat (_, _, _, _, a, b) ->
        vcat (compress a) (compress b)
    | qr -> qr

module SparseDouble =
    let sum = reduce (+) 0.0

    /// Compute the product of all values in a quad rope. This
    /// function short-circuits the computation if a branch evaluates
    /// to 0.
    let rec prod = function
        | HCat (_, _, _, _, Sparse (_, _, 0.0), _)
        | HCat (_, _, _, _, _, Sparse (_, _, 0.0))
        | VCat (_, _, _, _, Sparse (_, _, 0.0), _)
        | VCat (_, _, _, _, _, Sparse (_, _, 0.0))
        | Sparse (_, _, 0.0) -> 0.0
        | Sparse (_, _, 1.0) -> 1.0
        | HCat (_, _, _, _, a, b) ->
            let a' = prod a
            if a' = 0.0 then 0.0 else a' * prod b
        | VCat (_, _, _, _, a, b) ->
            let a' = prod a
            if a' = 0.0 then 0.0 else a' * prod b
        | Slice _ as qr -> prod (materialize qr)
        | qr -> reduce (*) 1.0 qr

    /// Initialize an identity matrix of h height and w width.
    let rec identity n =
        if n <= s_max then
            init n n (fun i j -> if i = j then 1.0 else 0.0)
        else
            let m = n >>> 1
            // Branching instead of bit-operations allows us to re-use
            // already allocated elements in the optimal case.
            if n &&& 1 = 1 then
                hnode (vnode (identity (m + 1)) (create m (m + 1) 0.0))
                         (vnode (create (m + 1) m 0.0) (identity m))
            else
                let sparse = Sparse (m, m, 0.0)
                let vals = identity m
                hnode (vnode vals sparse) (vnode sparse vals)

    /// Initialize an upper diagonal matrix with all non-zero values
    /// set to v.
    let rec upperDiagonal n v =
        if n <= s_max then
            init n n (fun i j -> if i < j then v else 0.0)
        else
            let m = n >>> 1
            let m' = if n &&& 1 = 1 then m + 1 else m
            // TODO: Optimize for re-use.
            hnode (vnode (upperDiagonal m v) (create m' m v))
                     (vnode (create m m' 0.0) (upperDiagonal m' v))

    /// Initialize a lower diagonal matrix with all non-zero values
    /// set to v.
    let rec lowerDiagonal n v =
        if n <= s_max then
            init n n (fun i j -> if i < j then 0.0 else v)
        else
            let m = n >>> 1
            let m' = if n &&& 1 = 1 then m + 1 else m
            // TODO: Optimize for re-use.
            hnode (vnode (lowerDiagonal m v) (create m' m 0.0))
                     (vnode (create m m' v) (lowerDiagonal m' v))

    /// Multiply two quad ropes of doubles point-wise.
    let pointwise (lqr : float QuadRope) (rqr : float QuadRope) : float QuadRope =
        /// General case for quad ropes of different structure.
        let rec gen lqr rqr =
            match lqr with
                | HCat (true, _, _, _, aa, ab) ->
                    let ba, bb = hsplit2 rqr (cols aa)
                    hnode (gen aa ba) (gen ab bb)
                | VCat (true, _, _, _, aa, ab) ->
                    let ba, bb = vsplit2 rqr (rows aa)
                    vnode (gen aa ba) (gen ab bb)
                | Sparse (_, _, 0.0) -> lqr
                | Sparse (_, _, 1.0) -> rqr
                | _ when isSparse rqr -> gen rqr lqr
                | _ -> zip (*) lqr rqr

        /// Fast case for quad ropes of equal structure.
        let rec fast lqr rqr =
            match lqr, rqr with
                // Recurse on nodes if at least one of them is sparse.
                | HCat (_,    _, _, _, aa, ab), HCat (true, _, _, _, ba, bb)
                | HCat (true, _, _, _, aa, ab), HCat (_,    _, _, _, ba, bb)
                    when shapesMatch aa ba && shapesMatch ab bb ->
                        hnode (fast aa ba) (fast ab bb)

                | VCat (_,    _, _, _, aa, ab), VCat (true, _, _, _, ba, bb)
                | VCat (true, _, _, _, aa, ab), VCat (_,    _, _, _, ba, bb)
                    when shapesMatch aa ba && shapesMatch ab bb ->
                        vnode (fast aa ba) (fast ab bb)

                // It may pay off to materialize if slices are sparse.
                | Slice _, _ when isSparse lqr -> fast (materialize lqr) rqr
                | _, Slice _ when isSparse rqr -> fast lqr (materialize rqr)

                // Sparse quad ropes of zero result in zero.
                | Sparse (_, _, 0.0), _ -> lqr
                | _, Sparse (_, _, 0.0) -> rqr

                // Sparse quad ropes of one result in the other quad rope.
                | Sparse (_, _, 1.0), _ -> rqr
                | _, Sparse (_, _, 1.0) -> lqr

                // Fall back to general case.
                | _ -> gen lqr rqr

        if not (shapesMatch lqr rqr) then
            invalidArg "rqr" "Quad ropes must be of equal shape."
        // Since multiplication is commutative, move the shallower
        // quad rope left to recurse on its structure instead of the
        // deeper one.
        if depth rqr < depth lqr then
            fast rqr lqr
        else
            fast lqr rqr

module SparseString =
    let cat = reduce (+) ""
