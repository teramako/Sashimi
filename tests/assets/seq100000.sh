#!/bin/sh
for i in $(seq 1 100000); do
  echo "line $i"
done &
sleep 0.001
# kill $!
