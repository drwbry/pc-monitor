#!/usr/bin/env bash
# ts-log-report.sh — summarise ThrottleStop daily logs.
#
# The "Step 6" verification report: temperature profile, VID at matched load,
# throttle reasons, and an AC vs battery split, for one or more days.
#
# Usage:
#   ./ts-log-report.sh                      # today
#   ./ts-log-report.sh 2026-08-23           # one day
#   ./ts-log-report.sh 2026-08-20 2026-08-23   # several (compare)
#   ./ts-log-report.sh --dir /some/path 2026-08-23
#   ./ts-log-report.sh /full/path/to/log.txt
#
# Log column layout (the NVIDIA GPU header spans TWO columns):
#   1 DATE  2 TIME  3 MULTI  4 C0%  5 CKMOD  6 BAT_mW  7 TEMP
#   8 GPUMHz  9 GPUtemp  10 VID  11 POWER  12+ throttle reasons
#
# Gotcha these files impose: they are CRLF, so the LAST token on each line
# carries a trailing \r. A reason tally keyed on $i silently splits into
# "TEMP" and "TEMP\r" as two separate keys. Every awk below strips it.

set -uo pipefail

LOGDIR="/mnt/c/Users/dreux/Desktop/Logs"
BOOST_MULTI=46      # "matched load" threshold for VID comparison

args=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --dir)   LOGDIR="$2"; shift 2 ;;
    --multi) BOOST_MULTI="$2"; shift 2 ;;
    -h|--help) sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) args+=("$1"); shift ;;
  esac
done

[[ ${#args[@]} -eq 0 ]] && args=("$(date +%F)")

resolve() {
  local a="$1"
  if [[ -f "$a" ]]; then echo "$a"
  elif [[ -f "$LOGDIR/$a" ]]; then echo "$LOGDIR/$a"
  elif [[ -f "$LOGDIR/$a.txt" ]]; then echo "$LOGDIR/$a.txt"
  else return 1; fi
}

# percentiles for a numeric stream on stdin
pct() {
  sort -n | awk '{v[n++]=$1}
    END{ if(!n){print "n/a"; exit}
      printf "n=%d med=%.4f p90=%.4f p95=%.4f max=%.4f",
        n, v[int(n*0.50)], v[int(n*0.90)], v[int(n*0.95)], v[n-1] }'
}

for a in "${args[@]}"; do
  f=$(resolve "$a") || { echo "!! no log found for '$a' (looked in $LOGDIR)"; echo; continue; }

  echo "=============================================================="
  echo " $(basename "$f")"
  echo "=============================================================="

  # --- coverage + temperature ---
  awk 'NR>1 && NF>=11 {
        n++; t=$7+0; s+=t; if(t>mx)mx=t
        if(t>=100)a++; if(t>=95)b++; if(t>=90)c++
        if(!first)first=$2; last=$2
      }
      END{ if(!n){print " no data rows"; exit}
        printf " samples   : %d   (%s -> %s)\n", n, first, last
        printf " temp      : mean %.1f C   max %d C\n", s/n, mx
        printf "             >=90C %6.2f%%   >=95C %6.2f%%   >=100C %6.2f%%\n",
               100*c/n, 100*b/n, 100*a/n
      }' "$f"

  # --- VID at matched load ---
  printf " VID @MULTI>=%s : " "$BOOST_MULTI"
  awk -v M="$BOOST_MULTI" 'NR>1 && NF>=11 && $3+0>=M { v=$10+0; if(v>0.5 && v<2.0) print v }' "$f" | pct
  echo
  awk -v M="$BOOST_MULTI" 'NR>1 && NF>=11 && $3+0>=M { n++; s+=$3+0 }
      END{ if(n) printf "             (avg MULTI in that set: %.2f)\n", s/n }' "$f"

  # --- throttle reasons ---
  printf " reasons   :"
  awk 'NR>1 && NF>=12 {
        for(i=12;i<=NF;i++){ x=$i; sub(/\r$/,"",x)
          if(x ~ /^[A-Z][A-Z0-9]*$/) r[x]++ }
      }
      END{ if(!length(r)){printf " none\n"; exit}
        for(k in r) printf " %s=%d", k, r[k]; printf "\n" }' "$f"

  # --- AC vs battery ---
  awk 'NR>1 && NF>=11 {
        arm = ($6+0!=0) ? "battery" : "AC     "
        n[arm]++; sm[arm]+=$3+0; sp[arm]+=$11+0
        v=$10+0; if(v>0.5&&v<2.0){ sv[arm]+=v; nv[arm]++ }
        if($3+0>mx[arm]) mx[arm]=$3+0
      }
      END{ for(k in n)
        printf " %s   : %6d samples  avgMULTI %5.2f  maxMULTI %5.2f  avgVID %.4f  avgPWR %4.1f W\n",
          k, n[k], sm[k]/n[k], mx[k], (nv[k]?sv[k]/nv[k]:0), sp[k]/n[k] }' "$f"
  echo
done
