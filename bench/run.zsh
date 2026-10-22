#!/bin/zsh
# Times each benchmark on forth, gforth, gforth-fast and pForth, and prints a Markdown table of the
# median wall time of ROUNDS runs. The systems take turns within each round, so a slow patch of the
# machine lands on all of them rather than on one.
#
#     zsh bench/run.zsh
#
# FORTH, GFORTH, GFORTH_FAST and PFORTH name the binaries; forth is built from this checkout at the
# manifest's -O2 when FORTH is not set. Every run's first line of output is checked against the
# others', so a system that computed something different stops the run.

emulate -L zsh
setopt err_exit pipe_fail
zmodload zsh/datetime

repo=${0:A:h:h}
rounds=${ROUNDS:-5}
scratch=$(mktemp -d)

if [[ -z $FORTH ]]; then
    sysl build $repo -o $scratch/forth > /dev/null 2>&1
    FORTH=$scratch/forth
fi

GFORTH=${GFORTH:-gforth}
GFORTH_FAST=${GFORTH_FAST:-gforth-fast}
PFORTH=${PFORTH:-pforth}

systems=(forth gforth gforth-fast pforth)
benches=(sieve fib nest bubble)

# One run of `bench` on `sys`; its first line of output goes to $scratch/out.
run_one() {
    local sys=$1 file=$repo/bench/$2.fs

    case $sys in
        forth)       $FORTH $file < /dev/null ;;
        gforth)      $GFORTH $file ;;
        gforth-fast) $GFORTH_FAST $file ;;
        pforth)      $PFORTH -q $file ;;
    esac > $scratch/full 2>&1 || true

    head -1 $scratch/full | tr -d ' ' > $scratch/out
}

# The median of the arguments, which are numbers.
median() {
    local sorted=(${(on)@})

    print ${sorted[$(( (${#sorted} + 1) / 2 ))]}
}

typeset -A times

for bench in $benches; do
    expected=""

    for (( r = 1; r <= rounds; r++ )); do
        for sys in $systems; do
            start=$EPOCHREALTIME
            run_one $sys $bench
            finish=$EPOCHREALTIME

            answer=$(< $scratch/out)

            if [[ -z $expected ]]; then
                expected=$answer
            elif [[ $answer != $expected ]]; then
                print -u2 "$bench: $sys printed '$answer', expected '$expected'"
                exit 1
            fi

            times[$bench.$sys]+=" $(( finish - start ))"
        done
    done
done

print "| benchmark | ${(j: | :)systems} |"
rule="|---|"

for sys in $systems; do
    rule+="---:|"
done

print $rule

for bench in $benches; do
    row="| $bench |"

    for sys in $systems; do
        row+=" $(printf '%.3f' $(median ${=times[$bench.$sys]})) |"
    done

    print $row
done

rm -rf $scratch
